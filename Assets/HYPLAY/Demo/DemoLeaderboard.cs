using System.Collections;
using HYPLAY.Core.Runtime;
using HYPLAY.Leaderboards.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HYPLAY.Demo
{
    public class DemoLeaderboard : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private HyplayLeaderboard leaderboard;
        [SerializeField] private float multiplier = 5f;
        [SerializeField, Range(0, 10)] private int numScoresToShow = 5;
        [SerializeField] private HyplayLeaderboard.OrderBy orderBy;
        [SerializeField] private TextMeshProUGUI[] scoreText;
        [SerializeField] private TextMeshProUGUI myScore;
        
        private bool _pressed = false;
        
        private float _score = 0;
        
        private void Awake()
        {
            HyplayBridge.LoggedIn += GetScores;
            if (HyplayBridge.IsLoggedIn)
                GetScores();
            
            if (leaderboard == null)
                Debug.LogError("Please select a leaderboard to use");
        }
        
        public async void SubmitScore()
        {
            if (leaderboard == null) return;
            var res = await leaderboard.PostScore(Mathf.RoundToInt(_score));
            if (res.Success)
                Debug.Log($"Successfully posted score {res.Data.score}");
            else
                Debug.LogError($"Update score failed: {res.Error}");
            
            GetScores();
        }
        
        private async void GetScores()
        {
            if (leaderboard == null) return;
            foreach (var text in scoreText)
                text.gameObject.SetActive(false);
            
            var scores = await leaderboard.GetScores(orderBy, 0, numScoresToShow);
            if (!scores.Success)
            {
                Debug.Log($"Getting scores failed: {scores.Error}");
                return;
            }
            for (var i = 0; i < scores.Data.scores.Length; i++)
            {
                var score = scores.Data.scores[i];
                var text = scoreText[i];
                text.gameObject.SetActive(true);
                text.text = $"{score.username} scored {score.score:F}";
            }
        }

        private void Update()
        {
            _score += Time.deltaTime * multiplier * (_pressed ? 1 : -1);
            if (_score < 0)
                _score = 0;
            else
                myScore.text = $"current score: {_score:0.00}";
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _pressed = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _pressed = false;
            SubmitScore();
        }
    }
}