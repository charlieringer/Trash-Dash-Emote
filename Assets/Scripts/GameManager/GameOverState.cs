using UnityEngine;
using UnityEngine.UI;
#if UNITY_ANALYTICS
using UnityEngine.Analytics;
#endif
using System.Collections.Generic;
 
/// <summary>
/// state pushed on top of the GameManager when the player dies.
/// </summary>
public class GameOverState : AState
{
    public TrackManager trackManager;
    public Canvas canvas;
	public Text scoreText;
	public AudioClip gameOverTheme;



    public GameObject addButton;

	protected bool m_CoinCredited = false;

    public override void Enter(AState from)
    {
        canvas.gameObject.SetActive(true);


		scoreText.text = "SCORE: " + trackManager.score.ToString();
//
//        if (PlayerData.instance.AnyMissionComplete())
//            missionPopup.Open();
//        else
//            missionPopup.gameObject.SetActive(false);

		m_CoinCredited = false;

		CreditCoins();

		if (MusicPlayer.instance.GetStem(0) != gameOverTheme)
		{
            MusicPlayer.instance.SetStem(0, gameOverTheme);
			StartCoroutine(MusicPlayer.instance.RestartAllStems());
        }
    }

	public override void Exit(AState to)
    {
        canvas.gameObject.SetActive(false);
        FinishRun();
    }

    public override string GetName()
    {
        return "GameOver";
    }

    public override void Tick()
    {
        
    }
		

	public void GoToStore()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene("shop", UnityEngine.SceneManagement.LoadSceneMode.Additive);
    }


    public void GoToLoadout()
    {
        trackManager.isRerun = false;
		manager.SwitchState("Loadout");
    }

    public void RunAgain()
    {
        trackManager.isRerun = false;
        manager.SwitchState("Game");
    }

    protected void CreditCoins()
	{
		if (m_CoinCredited)
			return;

		// -- give coins gathered
		PlayerData.instance.coins += trackManager.characterController.coins;
		PlayerData.instance.premium += trackManager.characterController.premium;

		PlayerData.instance.Save();

#if UNITY_ANALYTICS // Using Analytics Standard Events v0.3.0
        var transactionId = System.Guid.NewGuid().ToString();
        var transactionContext = "gameplay";
        var level = PlayerData.instance.rank.ToString();
        var itemType = "consumable";
        
        if (trackManager.characterController.coins > 0)
        {
            AnalyticsEvent.ItemAcquired(
                AcquisitionType.Soft, // Currency type
                transactionContext,
                trackManager.characterController.coins,
                "fishbone",
                PlayerData.instance.coins,
                itemType,
                level,
                transactionId
            );
        }

        if (trackManager.characterController.premium > 0)
        {
            AnalyticsEvent.ItemAcquired(
                AcquisitionType.Premium, // Currency type
                transactionContext,
                trackManager.characterController.premium,
                "anchovies",
                PlayerData.instance.premium,
                itemType,
                level,
                transactionId
            );
        }
#endif 

        m_CoinCredited = true;
	}

	protected void FinishRun()
    {


        CharacterCollider.DeathEvent de = trackManager.characterController.characterCollider.deathData;
        //register data to analytics


        PlayerData.instance.Save();

        trackManager.End();
    }

    //----------------
}
