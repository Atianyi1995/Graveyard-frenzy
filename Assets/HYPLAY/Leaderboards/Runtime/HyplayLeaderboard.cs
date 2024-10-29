using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HYPLAY.Core.Runtime;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HYPLAY.Leaderboards.Runtime
{
    public class HyplayLeaderboard : ScriptableObject
    {
        public string id;
        public new string name;
        public string description;
        public string secretKey;
        public OverwriteMode scoreType;
        public enum OverwriteMode { highest, lowest, latest }
        public enum OrderBy { descending, ascending }

        public async Task<HyplayResponse<LeaderboardScore>> GetCurrentUserScore()
        {
            var appId = HyplayBridge.GetSettings().Current.id;
            var req = UnityWebRequest.Get($"https://api.hyplay.com/v1/apps/{appId}/leaderboards/{id}/score?userId={HyplayBridge.CurrentUser.Id}");
            await req.SendWebRequest();
            if (req.responseCode != 200)
            {
                return new HyplayResponse<LeaderboardScore>
                {
                    Data = null,
                    Error = req.downloadHandler.text
                };
            }

            var res = HyplayJSON.Deserialize<LeaderboardScore>(req.downloadHandler.text);
            var error = req.downloadHandler.error;
            return new HyplayResponse<LeaderboardScore>
            {
                Data = res,
                Error = error
            };
        }
        
        public async Task<HyplayResponse<LeaderboardScores>> GetScores(OrderBy sort = OrderBy.descending, int offset = 0, int limit = 25)
        {
            var appId = HyplayBridge.GetSettings().Current.id;
            var req = UnityWebRequest.Get($"https://api.hyplay.com/v1/apps/{appId}/leaderboards/{id}/scores?sort={sort}&offset={offset}&limit={limit}");
            await req.SendWebRequest();
            if (req.responseCode != 200)
            {
                return new HyplayResponse<LeaderboardScores>
                {
                    Data = null,
                    Error = req.downloadHandler.text
                };
            }

            var res = HyplayJSON.Deserialize<LeaderboardScores>(req.downloadHandler.text);
            var error = req.downloadHandler.error;
            return new HyplayResponse<LeaderboardScores>
            {
                Data = res,
                Error = error
            };
        }

        public async Task<HyplayResponse<LeaderboardResponse>> PostScore (int score)
        {
            var userReq = await HyplayBridge.GetUserAsync();
            if (!userReq.Success)
            {
                HyplayBridge.ClearUser();
                return new HyplayResponse<LeaderboardResponse>
                {
                    Data = null,
                    Error = "Not logged in"
                };
            }

            var user = userReq.Data;
            
            var dataToHash = $"{secretKey}:{user.Id}:{score.ToString(CultureInfo.InvariantCulture)}:{scoreType.ToString()}";
            //Debug.Log(dataToHash);
            using var sha256Hash = SHA256.Create();

            // Convert the input string to a byte array and compute the hash
            var data = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(dataToHash));

            // Create a new StringBuilder to collect the bytes
            // and create a string
            var sBuilder = new StringBuilder();

            // Loop through each byte of the hashed data 
            // and format each one as a hexadecimal string
            foreach (var b in data)
                sBuilder.Append(b.ToString("x2"));

            // Get the hexadecimal string representation of the hash
            var hash = sBuilder.ToString();

            var body = new Dictionary<string, object>
            {
                { "score", score },
                { "hash", hash },
                { "scoreType", scoreType.ToString() }
            };
            
            //Debug.Log(HyplayJSON.Serialize(body));
            
            using var req = UnityWebRequest.Post(
                $"https://api.hyplay.com/v1/apps/{HyplayBridge.GetSettings().Current.id}/leaderboards/{id}/scores",
                HyplayJSON.Serialize(body)
                , "application/json");

            if (!HyplayBridge.SetAuthHeader(req))
            {
                HyplayBridge.ClearUser();
                return new HyplayResponse<LeaderboardResponse>
                {
                    Data = null,
                    Error = "Not logged in"
                };
            }

            await req.SendWebRequest();

            if (req.responseCode != 200)
            {
                return new HyplayResponse<LeaderboardResponse>
                {
                    Data = null,
                    Error = req.downloadHandler.text
                };
            }

            var res = HyplayJSON.Deserialize<LeaderboardResponse>(req.downloadHandler.text);
            var error = req.downloadHandler.error;
            return new HyplayResponse<LeaderboardResponse>
            {
                Data = res,
                Error = error
            };
        }

        #if UNITY_EDITOR
        public async Task GetSecret(string appId, string appSecret)
        {
            using var req = UnityWebRequest.Get($"https://api.hyplay.com/v1/apps/{appId}/leaderboards/{id}/key");
            req.SetRequestHeader("x-app-authorization", appSecret);

            await req.SendWebRequest();
            secretKey = req.downloadHandler.text.Replace("\"", "");
            
            EditorUtility.SetDirty(this); 
            AssetDatabase.SaveAssets();
        }

        public async void UpdateLeaderboard()
        {
            var settings = HyplayBridge.GetSettings();
            var body = new Dictionary<string, object>
            {
                { "name", name },
                { "description", description }
            };
            using var req = UnityWebRequest.Post($"https://api.hyplay.com/v1/apps/{settings.Current.id}/leaderboards/{id}",
                HyplayJSON.Serialize(body)
                ,"application/json");
            
            req.SetRequestHeader("x-app-authorization", settings.Current.secretKey);
            req.method = "PATCH";

            await req.SendWebRequest();
        }
        #endif
    }
}

[Serializable]
public class LeaderboardResponse
{
    public double score;
}

[Serializable]
public class LeaderboardScores
{
    public LeaderboardScore[] scores;
}

[Serializable]
public class LeaderboardScore
{
    public string userId;
    public string username;
    public double score;
}