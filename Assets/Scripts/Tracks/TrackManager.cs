using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using Affdex;
#if UNITY_ANALYTICS
using UnityEngine.Analytics;
#endif

/// <summary>
/// The TrackManager handles creating track segments, moving them and handling the whole pace of the game.
/// 
/// The cycle is as follows:
/// - Begin is called when the game starts.
///     - if it's a first run, init the controller, collider etc. and start the movement of the track.
///     - if it's a rerun (after watching ads on GameOver) just restart the movement of the track.
/// - Update moves the character and - if the character reaches a certain distance from origin (given by floatingOriginThreshold) -
/// moves everything back by that threshold to "reset" the player to the origin. This allow to avoid floating point error on long run.
/// It also handles creating the tracks segements when needed.
/// 
/// If the player has no more lives, it pushes the GameOver state on top of the GameState without removing it. That way we can just go back to where
/// we left off if the player watches an ad and gets a second chance. If the player quits, then:
/// 
/// - End is called and everything is cleared and destroyed, and we go back to the Loadout State.
/// </summary>
public class TrackManager : MonoBehaviour
{
	int numbSegsMade = 0;
	static public TrackManager instance { get { return s_Instance; } }
	static protected TrackManager s_Instance;

    static int s_StartHash = Animator.StringToHash("Start");

	public delegate int MultiplierModifier(int current);
	public MultiplierModifier modifyMultiply;

	[Header("Character & Movements")]
	public CharacterInputController characterController;
	public float minSpeed = 10.0f;
	public float maxSpeed = 18.0f;
	public int speedStep = 8;
	public float laneOffset = 1.0f;

	public bool invincible = false;

	[Header("Objects")]
	public ConsumableDatabase consumableDatabase;
	public MeshFilter skyMeshFilter;

	[Header("Parallax")]
	public Transform parallaxRoot;
	public float parallaxRatio = 0.5f;

	public int trackSeed {  get { return m_TrackSeed; } set { m_TrackSeed = value; } }

    public float timeToStart { get { return m_TimeToStart; } }  // Will return -1 if already started (allow to update UI)

	public int score { get { return m_Score; } }
	public int multiplier {  get { return m_Multiplier; } }
	public float worldDistance {  get { return m_TotalWorldDistance; } }
	public float speed {  get { return m_Speed; } }
	public float speedRatio {  get { return (m_Speed - minSpeed) / (maxSpeed - minSpeed); } }

	public TrackSegment currentSegment { get { return m_Segments[0]; } }
	public List<TrackSegment> segments { get { return m_Segments; } }
	public ThemeData currentTheme { get { return m_CurrentThemeData; } }

	public bool isMoving {  get { return m_IsMoving; } }
	public bool isRerun { get { return m_Rerun; } set { m_Rerun = value; } }

	public Listener emotionListener;

	protected float m_TimeToStart = -1.0f;

	// If this is set to -1, random seed is init to system clock, otherwise init to that value
	// Allow to play the same game multiple time (useful to make specific competition/challenge fair between players)
	protected int m_TrackSeed = -1;

	protected float m_CurrentSegmentDistance;
	protected float m_TotalWorldDistance;
	protected bool m_IsMoving;
	protected float m_Speed;

    protected float m_TimeSincePowerup;     // The higher it goes, the higher the chance of spawning one
	protected float m_TimeSinceLastPremium;

	protected int m_Multiplier;

	protected List<TrackSegment> m_Segments = new List<TrackSegment>();
	protected List<TrackSegment> m_PastSegments = new List<TrackSegment>();
	protected int m_SafeSegementLeft;

	protected ThemeData m_CurrentThemeData;
	protected int m_CurrentZone;
	protected float m_CurrentZoneDistance;
	protected int m_PreviousSegment = -1;

	protected int m_Score;
	protected float m_ScoreAccum;
    protected bool m_Rerun;     // This lets us know if we are entering a game over (ads) state or starting a new game (see GameState)

    const float k_FloatingOriginThreshold = 10000f;

    protected const float k_CountdownToStartLength = 5f;
    protected const float k_CountdownSpeed = 1.5f;
    protected const float k_StartingSegmentDistance = 2f;
    protected const int k_StartingSafeSegments = 2;
    protected const int k_StartingCoinPoolSize = 256;
    protected const int k_DesiredSegmentCount = 10;
    protected const float k_SegmentRemovalDistance = -30f;
    protected const float k_Acceleration = 0.2f;

	//UCB1 Stuff
	private int[,] exploreWeights;
	private float[,] exploitWeights;

	private List<float>[] currentSectionEmotionValues = new List<float>[9];

	private int playerID;
	private int gameType;
	private bool isAdaptive = true;

	public Text idText;
	public Text adaptiveText;

	private int currentGame = 0;

	//private int currentSegmentID = 0;
	private int segmentsPassed = 0;

	private string allDataOut = "";
	private string summaryDataOut = "";

	private int[] keysPressed = new int[2];

	private bool trippedThisSegment = false;

	private int sectionStartCoinScore = 0;

	public List<int> numCoinsPerSection = new List<int> ();
	public List<int> segmentIDs = new List<int> ();
	private float previousCoinCollectionPercentage = 1f;

	private int prevSectionLife;


    protected void Awake()
	{
        m_ScoreAccum = 0.0f;
		s_Instance = this;

		int numbTrackSections = m_CurrentThemeData.zones [m_CurrentZone].prefabList.Length;
		exploreWeights = new int[numbTrackSections,numbTrackSections];
		exploitWeights = new float[numbTrackSections,numbTrackSections];

		for (int i = 0; i < numbTrackSections; i++) {
			for (int j = 0; j < numbTrackSections; j++) {
				exploreWeights [i,j] = 0;
				exploitWeights [i,j] = 0f;
			}
		}
		for (int i = 0; i < currentSectionEmotionValues.Length; i++) {
			currentSectionEmotionValues [i] = new List<float> ();
		}
    }

	public void StartMove(bool isRestart = true)
	{
		m_IsMoving = true;
		if(isRestart)
			m_Speed = minSpeed;
	}

	public void StopMove()
	{
		m_IsMoving = false;
	}

	IEnumerator WaitToStart()
	{
		characterController.character.animator.Play(s_StartHash);
		float length = k_CountdownToStartLength;
		m_TimeToStart = length;

		while(m_TimeToStart >= 0)
		{
			yield return null;
			m_TimeToStart -= Time.deltaTime * k_CountdownSpeed;
		}

		m_TimeToStart = -1;

		if (m_Rerun)
		{
			// Make invincible on rerun, to avoid problems if the character died in front of an obstacle
			characterController.characterCollider.SetInvincible();
		}

		characterController.StartRunning();
		StartMove();
	}

	public void Begin()
	{
		prevSectionLife = characterController.currentLife;
		numbSegsMade = 0;
		segmentsPassed = 0;

		keysPressed = new int[2];
		trippedThisSegment = false;
		sectionStartCoinScore = 0;
		numCoinsPerSection = new List<int> ();
		segmentIDs = new List<int> ();
		previousCoinCollectionPercentage = 1f;
		prevSectionLife = 3;

		currentGame++;
		playerID = idText.text == "" ? 999 : int.Parse (idText.text);
		gameType = adaptiveText.text == "" ? 1 : int.Parse (adaptiveText.text);
		isAdaptive = gameType != 0;

		allDataOut = playerID + "_" + gameType + "_all.csv";
		summaryDataOut = playerID + "_" + gameType + "_summary.csv";

		if (currentGame == 1) {
			using (System.IO.StreamWriter file = 
				new System.IO.StreamWriter ((summaryDataOut), true)) {
				file.WriteLine ("Game, Section, Current_Section_ID, PCG_Section_ID, PCG_NextID, UCTScore, ExploitScore, ExploreScore, Joy, Fear, Disgust, Sadness,Anger, Suprise, Contempt, Valence, Engagement, Game Score\n");
			}
			using (System.IO.StreamWriter file = 
				new System.IO.StreamWriter ((allDataOut), true)) {
				file.WriteLine ("Game, Section, Current_Section_ID, Joy, Fear, Disgust, Sadness,Anger, Suprise, Contempt, Valence, Engagement\n");
			}
		}


		if (!m_Rerun)
		{
			if (m_TrackSeed != -1)
				Random.InitState(m_TrackSeed);
			else
				Random.InitState((int)System.DateTime.Now.Ticks);

			// Since this is not a rerun, init the whole system (on rerun we want to keep the states we had on death)
			m_CurrentSegmentDistance = k_StartingSegmentDistance;
			m_TotalWorldDistance = 0.0f;

            characterController.gameObject.SetActive(true);

            // Spawn the player
            Character player = Instantiate(CharacterDatabase.GetCharacter(PlayerData.instance.characters[PlayerData.instance.usedCharacter]), Vector3.zero, Quaternion.identity);
			player.transform.SetParent(characterController.characterCollider.transform, false);
			Camera.main.transform.SetParent(characterController.transform, true);


            player.SetupAccesory(PlayerData.instance.usedAccessory);

			characterController.character = player;
			characterController.trackManager = this;

			characterController.Init();
			characterController.CheatInvincible(invincible);

            m_CurrentThemeData = ThemeDatabase.GetThemeData(PlayerData.instance.themes[PlayerData.instance.usedTheme]);
			m_CurrentZone = 0;
			m_CurrentZoneDistance = 0;

			skyMeshFilter.sharedMesh = m_CurrentThemeData.skyMesh;
			RenderSettings.fogColor = m_CurrentThemeData.fogColor;
			RenderSettings.fog = true;
            
            gameObject.SetActive(true);
			characterController.gameObject.SetActive(true);
			characterController.coins = 0;
			characterController.premium = 0;
        
            m_Score = 0;
			m_ScoreAccum = 0;

            m_SafeSegementLeft = k_StartingSafeSegments;

            Coin.coinPool = new Pooler(currentTheme.collectiblePrefab, k_StartingCoinPoolSize);

        }

        characterController.Begin();
		StartCoroutine(WaitToStart());
	}

	public void End()
	{
	    foreach (TrackSegment seg in m_Segments)
	    {
	        Destroy(seg.gameObject);
	    }

	    for (int i = 0; i < m_PastSegments.Count; ++i)
	    {
	        Destroy(m_PastSegments[i].gameObject);
	    }

		m_Segments.Clear();
		m_PastSegments.Clear();

		characterController.End();

		gameObject.SetActive(false);
		Destroy(characterController.character.gameObject);
		characterController.character = null;

        Camera.main.transform.SetParent(null);

        characterController.gameObject.SetActive(false);

		for (int i = 0; i < parallaxRoot.childCount; ++i) 
		{
			Destroy (parallaxRoot.GetChild(i).gameObject);
		}

		//if our consumable wasn't used, we put it back in our inventory
		if (characterController.inventory != null) 
		{
            PlayerData.instance.Add(characterController.inventory.GetConsumableType());
			characterController.inventory = null;
		}
	}


	void Update ()
	{
		if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow))
		{
			keysPressed [1]++;
		}
		//Update the full frame by frame emotion values
		for (int i = 0; i < 9; i++) {
			currentSectionEmotionValues[i].Add(emotionListener.currentEmotions[(Emotions)i]);
		}

		int currentSeg = segmentIDs.Count > 0 ? segmentIDs [0] : -1;
		string outString = currentGame + "," + (segmentsPassed+1) + "," + currentSeg + ",";

		foreach (var emotion in emotionListener.currentEmotions) {
			outString += emotion.Value + ",";
		}
		outString += "\n";

		using (System.IO.StreamWriter file = 
			new System.IO.StreamWriter ((allDataOut), true)) {
			file.WriteLine (outString);
		}

        while (m_Segments.Count < k_DesiredSegmentCount)
		{
			SpawnNewSegment();
		}

		if (parallaxRoot != null && currentTheme.cloudPrefabs.Length > 0)
		{
			while (parallaxRoot.childCount < currentTheme.cloudNumber)
			{
				float lastZ = parallaxRoot.childCount == 0 ? 0 : parallaxRoot.GetChild(parallaxRoot.childCount - 1).position.z + currentTheme.cloudMinimumDistance.z;

				GameObject obj = Instantiate(currentTheme.cloudPrefabs[Random.Range(0, currentTheme.cloudPrefabs.Length)]);
				obj.transform.SetParent(parallaxRoot, false);

				obj.transform.localPosition = 
					Vector3.up * (currentTheme.cloudMinimumDistance.y + (Random.value - 0.5f) * currentTheme.cloudSpread.y) 
					+ Vector3.forward * (lastZ  + (Random.value - 0.5f) * currentTheme.cloudSpread.z)
					+ Vector3.right * (currentTheme.cloudMinimumDistance.x + (Random.value - 0.5f) * currentTheme.cloudSpread.x);

				obj.transform.localScale = obj.transform.localScale * (1.0f + (Random.value - 0.5f) * 0.5f);
				obj.transform.localRotation = Quaternion.AngleAxis(Random.value * 360.0f, Vector3.up);
			}
		}

		if (!m_IsMoving)
			return;

		float scaledSpeed = m_Speed * Time.deltaTime;
		m_ScoreAccum += scaledSpeed;
		m_CurrentZoneDistance += scaledSpeed;

		int intScore = Mathf.FloorToInt(m_ScoreAccum);
		if (intScore != 0) AddScore(intScore);
		m_ScoreAccum -= intScore;

		m_TotalWorldDistance += scaledSpeed;
		m_CurrentSegmentDistance += scaledSpeed;

		if(m_CurrentSegmentDistance > m_Segments[0].worldLength)
		{
			m_CurrentSegmentDistance -= m_Segments[0].worldLength;
			segmentsPassed++;
			// m_PastSegments are segment we already passed, we keep them to move them and destroy them later 
			// but they aren't part of the game anymore 

			updateExploitScoreForSegement ();

			sectionStartCoinScore = characterController.coins;


			m_PreviousSegment = segmentIDs[0];
			segmentIDs.RemoveAt (0);

			keysPressed [0] = keysPressed [1];
			keysPressed [1] = 0;
			m_PastSegments.Add(m_Segments[0]);


			m_Segments.RemoveAt(0);

		}

		Vector3 currentPos;
		Quaternion currentRot;
		Transform characterTransform = characterController.transform;

		m_Segments[0].GetPointAtInWorldUnit(m_CurrentSegmentDistance, out currentPos, out currentRot);


		// Floating origin implementation
        // Move the whole world back to 0,0,0 when we get too far away.
		bool needRecenter = currentPos.sqrMagnitude > k_FloatingOriginThreshold;

		// Parallax Handling
		if (parallaxRoot != null)
		{
			Vector3 difference = (currentPos - characterTransform.position) * parallaxRatio; ;
			int count = parallaxRoot.childCount;
			for (int i = 0; i < count; i++)
			{
				Transform cloud = parallaxRoot.GetChild(i);
				cloud.position += difference - (needRecenter ? currentPos : Vector3.zero);
			}
		}

		if (needRecenter)
        {
			int count = m_Segments.Count;
			for(int i = 0; i < count; i++)
            {
				m_Segments[i].transform.position -= currentPos;
			}

			count = m_PastSegments.Count;
			for(int i = 0; i < count; i++)
            {
				m_PastSegments[i].transform.position -= currentPos;
			}

			// Recalculate current world position based on the moved world
			m_Segments[0].GetPointAtInWorldUnit(m_CurrentSegmentDistance, out currentPos, out currentRot);
		}

		characterTransform.rotation = currentRot;
		characterTransform.position = currentPos;

		if(parallaxRoot != null && currentTheme.cloudPrefabs.Length > 0)
		{
			for(int i = 0; i < parallaxRoot.childCount; ++i)
			{
				Transform child = parallaxRoot.GetChild(i);

				// Destroy unneeded clouds
				if ((child.localPosition - currentPos).z < -50)
					Destroy(child.gameObject);
			}
		}

		// Still move past segment until they aren't visible anymore.
		for(int i = 0; i < m_PastSegments.Count; ++i)
		{
            if ((m_PastSegments[i].transform.position - currentPos).z < k_SegmentRemovalDistance)
			{
				m_PastSegments[i].Cleanup();
				m_PastSegments.RemoveAt(i);
				i--;
			}
		}

		PowerupSpawnUpdate();

		if (m_Speed < maxSpeed)
            m_Speed += k_Acceleration * Time.deltaTime;
		else
			m_Speed = maxSpeed;

        m_Multiplier = 1 + Mathf.FloorToInt((m_Speed - minSpeed) / (maxSpeed - minSpeed) * speedStep);

        if (modifyMultiply != null)
        {
            foreach (MultiplierModifier part in modifyMultiply.GetInvocationList())
            {
                m_Multiplier = part(m_Multiplier);
            }
        }

        //check for next rank achieved
        int currentTarget = (PlayerData.instance.rank + 1) * 300;
        if(m_TotalWorldDistance > currentTarget)
        {
            PlayerData.instance.rank += 1;
            PlayerData.instance.Save();
        }
        MusicPlayer.instance.UpdateVolumes(speedRatio);
    }

    public void PowerupSpawnUpdate()
	{
		m_TimeSincePowerup += Time.deltaTime;
		m_TimeSinceLastPremium += Time.deltaTime;
	}

	public void SpawnNewSegment()
	{
		numbSegsMade++;
		int nextSegmentID = getNextSegment();
		segmentIDs.Add (nextSegmentID);
		TrackSegment segmentToUse = m_CurrentThemeData.zones[m_CurrentZone].prefabList[nextSegmentID];
		TrackSegment newSegment = Instantiate(segmentToUse, Vector3.zero, Quaternion.identity);

		Vector3 currentExitPoint;
		Quaternion currentExitRotation;
		if (m_Segments.Count > 0)
		{
			m_Segments[m_Segments.Count - 1].GetPointAt(1.0f, out currentExitPoint, out currentExitRotation);
		}
		else
		{
			currentExitPoint = transform.position;
			currentExitRotation = transform.rotation;
		}

		newSegment.transform.rotation = currentExitRotation;

		Vector3 entryPoint;
		Quaternion entryRotation;
		newSegment.GetPointAt(0.0f, out entryPoint, out entryRotation);


		Vector3 pos = currentExitPoint + (newSegment.transform.position - entryPoint);
		newSegment.transform.position = pos;
		newSegment.manager = this;

		//newSegment.transform.localScale = new Vector3((Random.value > 0.5f ? -1 : 1), 1, 1);
		//newSegment.objectRoot.localScale = new Vector3(1.0f/newSegment.transform.localScale.x, 1, 1);

		if (m_SafeSegementLeft <= 0)
			SpawnObstacle(newSegment);
		else
			m_SafeSegementLeft -= 1;

		m_Segments.Add(newSegment);
	}

	public int getNextSegment()
	{
		if(numbSegsMade < k_DesiredSegmentCount)
			return Random.Range (0, m_CurrentThemeData.zones [m_CurrentZone].prefabList.Length);
		int totalExplore = 0;

		for(int i = 0; i <  m_CurrentThemeData.zones[m_CurrentZone].prefabList.Length; i++) {
			totalExplore += exploreWeights[segmentIDs [segmentIDs.Count-1], i];
		}

		List<int> possibleIndexs = new List<int>();
		float bestScore = -0f;
		List<int> unvisited = new List<int> ();

		for (int i = 0; i <  m_CurrentThemeData.zones[m_CurrentZone].prefabList.Length; i++) {

			float score = exploitWeights[segmentIDs [segmentIDs.Count-1], i] + (Mathf.Sqrt(2) * Mathf.Sqrt(Mathf.Log(totalExplore)/exploreWeights[segmentIDs [segmentIDs.Count-1], i]+Mathf.Epsilon));
			if (exploreWeights [segmentIDs [segmentIDs.Count - 1], i] == 0)
				unvisited.Add (i); //If we have not explored this we want to
			if (score > bestScore) {
				bestScore = score;
				possibleIndexs.Clear ();
				possibleIndexs.Add (i);
			} else if (score == bestScore) {
				possibleIndexs.Add (i);
			}
		}

		int nextSegment = unvisited.Count == 0 ? possibleIndexs [Random.Range (0, possibleIndexs.Count)] : unvisited [Random.Range (0, unvisited.Count)];

		//print ("Current segement " + currentSegmentID +  " Best next segment: " + nextSegment + " with score of: " + bestScore);
		string outString = currentGame + "," + (segmentsPassed) + "," + segmentIDs [0] + "," + segmentIDs [segmentIDs.Count-1] + "," + nextSegment + "," + bestScore + "," + exploitWeights [segmentIDs [segmentIDs.Count-1], nextSegment] + ","+ (Mathf.Sqrt(Mathf.Log(totalExplore)/((float)exploreWeights[segmentIDs [segmentIDs.Count-1], nextSegment]+Mathf.Epsilon))) + ",";
		foreach (List<float> emotions in currentSectionEmotionValues) {
			float total = 0f;
			foreach (float emotion in emotions) {
				total += emotion;
			}
			total /= emotions.Count;
			outString += total + ",";
		}
		outString += score +  "\n";

		if (segmentsPassed != 0) {
			using (System.IO.StreamWriter file = 
				      new System.IO.StreamWriter ((summaryDataOut), true)) {
				file.WriteLine (outString);
			}
		}


		if (!isAdaptive)
			return Random.Range (0, m_CurrentThemeData.zones [m_CurrentZone].prefabList.Length);
		return nextSegment;//return a random best index
	}

	public void updateExploitScoreForSegement()
	{
		//Don't update if we are on a safe segment
		if (segmentsPassed < 3)
			return; 
		
		float newScore = (gameType == 1) ? getCurrentScore () : getCurrentScore_GBPEM ();
		float oldScore = exploitWeights [m_PreviousSegment, segmentIDs [0]] * exploreWeights [m_PreviousSegment, segmentIDs [0]];


		float score = exploreWeights [m_PreviousSegment, segmentIDs [0]] == 0 ? newScore : (oldScore + newScore) / (exploreWeights [m_PreviousSegment, segmentIDs [0]] + 1);
		exploitWeights [m_PreviousSegment, segmentIDs [0]] = score;
		exploreWeights [m_PreviousSegment, segmentIDs [0]] += 1;

		return; 
	}


	public float getCurrentScore()
	{
		float totalVal = 0f;
		foreach (float val in currentSectionEmotionValues[7]) {
			totalVal += val;
		}
		totalVal /= currentSectionEmotionValues[7].Count;


		float totalEng = 0f;
		foreach (float eng in currentSectionEmotionValues[8]) {
			totalEng += eng;
		}
		totalEng /= currentSectionEmotionValues[8].Count;

		for(int i = 0; i < currentSectionEmotionValues.Length; i++)
		{
			currentSectionEmotionValues [i] = new List<float> ();
		}

		totalVal = (totalVal + 100) / 2; //rescale values this will be between 0-100
		totalEng = (totalEng + 100) / 2; //rescale values this will be between 50-100 (because eng is between 0-100 but we want a center at 50)

		float total = (totalVal + totalEng)-100; //Now the summed values are between -50 - 100 (but we can "assume" it is between -100 - 100, 0 Val and 0 Eng = 0 in this scale)
		total *= 0.1f; //now between -5 - 10

		total = 1 / (1 + Mathf.Exp (-total)); 
		return total;
	}

	public float getCurrentScore_GBPEM()
	{
		float total = 0f;

		float keypressedScore = 0f;
		if (!trippedThisSegment){
			keypressedScore = 1f-(Mathf.Abs(4-(keysPressed [0] + keysPressed [1]))*0.25f);
			if (keypressedScore > 1f)
				keypressedScore = 1f;
			if (keypressedScore < 0f)
				keypressedScore = 0f;
		}
		total += keypressedScore;

		float maxCoinsCollected = numCoinsPerSection[0];
		float coinscollected = characterController.coins - sectionStartCoinScore;
		float percentCoinsCollected = coinscollected / maxCoinsCollected;
		float avgPercentCoinsCollected = (percentCoinsCollected + previousCoinCollectionPercentage) / 2;
		previousCoinCollectionPercentage = percentCoinsCollected;

		total += avgPercentCoinsCollected;

		float survivedScore = characterController.currentLife < prevSectionLife ? 0f : 1f;
		prevSectionLife = characterController.currentLife;

		total += survivedScore;
		total /= 3;
		print (total);
		//print ("Score: " + total + " coin score: " + avgPercentCoinsCollected + " coins collected " + coinscollected + "total coins " + maxCoinsCollected + " old percentage " + previousCoinCollectionPercentage  );
		numCoinsPerSection.RemoveAt (0);
		return total;
	}


	public void SpawnObstacle(TrackSegment segment)
	{
		if(segment.hasLowBarrier) segment.possibleObstacles[3].Spawn(segment, 0.5f, -1);
		if(segment.hasHighBarrier) segment.possibleObstacles[2].Spawn(segment, 0.5f, -1);
		for (int i = 0; i < segment.obstaclesPresent.Length; ++i)
		{
			if (segment.obstaclesPresent [i] == -1)
				continue;
			segment.possibleObstacles[segment.obstaclesPresent[i]].Spawn(segment, 0.5f, i-1);
		}
		SpawnCoinAndPowerup(segment);
	}

	public void SpawnCoinAndPowerup(TrackSegment segment)
	{
		int numCoinsSpawned = 0;
		const float increment = 1.5f;
		float currentWorldPos = 0.0f;
		int currentLane = segment.GetComponent<TrackSegment>().coinLane;

		float powerupChance = Mathf.Clamp01(Mathf.Floor(m_TimeSincePowerup) * 0.5f * 0.001f);

		while (currentWorldPos < segment.worldLength)
		{
			Vector3 pos;
			Quaternion rot;
			segment.GetPointAtInWorldUnit(currentWorldPos, out pos, out rot);

			pos = pos + ((currentLane - 1) * laneOffset * (rot * Vector3.right));

            GameObject toUse;
			if (Random.value < powerupChance)
			{
                int picked = Random.Range(0, consumableDatabase.consumbales.Length);

                //if the powerup can't be spawned, we don't reset the time since powerup to continue to have a high chance of picking one next track segment
                if (consumableDatabase.consumbales[picked].canBeSpawned)
                {
                    // Spawn a powerup instead.
                    m_TimeSincePowerup = 0.0f;
                    powerupChance = 0.0f;

                    toUse = Instantiate(consumableDatabase.consumbales[picked].gameObject, pos, rot) as GameObject;
                    toUse.transform.SetParent(segment.transform, true);
                }
			}
			else
			{
				toUse = Coin.coinPool.Get(pos, rot);
				toUse.transform.SetParent(segment.collectibleTransform, true);
				numCoinsSpawned++;
			}
			currentWorldPos += increment;
		}
		numCoinsPerSection.Add (numCoinsSpawned);
	}


    public void AddScore(int amount)
	{
		int finalAmount = amount;
		m_Score += finalAmount * m_Multiplier;
	}
}