using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace HYPLAY.Core.Runtime
{
    // todo: handle cancelled/other errors https://api.wayfarer.games/hyplay-test/redirect.html?error=cancelled
    
    public class HyplaySettings : ScriptableObject
    {
        #if UNITY_EDITOR
        [SerializeField, Space] private string accessToken;
        public string AccessToken => accessToken;
        
        [SerializeField] private string appName;
        [SerializeField] private string appDescription;
        [SerializeField] private string appUrl;
        #endif

        [SerializeField, Tooltip("How many hours after logging in will the token expire?")] 
        private int timeoutHours = 24;
        public int TimeoutHours => timeoutHours;

        [SerializeField, Tooltip("Use a popup window? If disabled, redirects the whole page")]
        private bool usePopup = true;

        [SerializeField] private HyplayApp currentApp;
        public HyplayApp Current => currentApp;
        
        public string Token { get; private set; }

        #if UNITY_WEBGL
        [DllImport("__Internal")]
        private static extern void DoLoginRedirect(string appid, string expiry);
        [DllImport("__Internal")]
        private static extern void DoLoginPopup(string appid, string expiry);
        #endif
        
        public void SetCurrent(HyplayApp current)
        {
            currentApp = current;
        }

        public void SetToken(string token)
        {
            Token = token;
        }

        internal async void DoLogin()
        {
            var time = DateTimeOffset.Now + TimeSpan.FromHours(timeoutHours);
            
            #if UNITY_WEBGL && !UNITY_EDITOR
            if (usePopup)
                DoLoginPopup(Current.id, $"&expiresAt={time.ToUnixTimeSeconds()}");
            else
                DoLoginRedirect(Current.id, $"&expiresAt={time.ToUnixTimeSeconds()}");
            #else
            
            
            #if !UNITY_EDITOR
            if (Application.platform != RuntimePlatform.Android && Application.platform != RuntimePlatform.IPhonePlayer)
            {
                Debug.LogError("HYPLAY currently does not work for standalone builds.");
                return;
            }
            var redirectUri = Current.redirectUris.First(uri => !uri.Contains("http")) + $"&expiresAt={time.ToUnixTimeSeconds()}";
            var url = "https://hyplay.com/oauth/authorize/?appId=" + Current.id + "&chain=HYCHAIN&responseType=token&redirectUri=" + redirectUri;
            Application.OpenURL(url);
            #else
            
            var body = new Dictionary<string, object>
            {
                { "appId", currentApp.id },
                { "deadline", time.ToUnixTimeSeconds() },
                { "responseType", "token" },
                { "chain", "HYCHAIN"},
                { "nonce", Guid.NewGuid() }
            };

            #if UNITY_2022_1_OR_NEWER
            using var req = UnityWebRequest.Post($"https://api.hyplay.com/v1/sessions", HyplayJSON.Serialize(body), "application/json");
            #else
            using var req = UnityWebRequest.Post($"https://api.hyplay.com/v1/sessions", "");
            HyplayJSON.SetData(ref req, HyplayJSON.Serialize(currentApp));
            #endif
            
            req.SetRequestHeader("x-authorization", accessToken);
            await req.SendWebRequest();

            var res = JsonUtility.FromJson<SessionCreateResponse>(req.downloadHandler.text);

            if (string.IsNullOrWhiteSpace(res.accessToken))
            {
                Debug.LogError("No access token recieved, check your access token starts with user_at_ in the settings, and make sure you have an app set up.");
                return;
            }
            HyplayBridge.DeepLink($"myapp://token#token={res.accessToken}");
            #endif
            
            #endif
        }
        
        #if UNITY_EDITOR
        public async Task<List<HyplayApp>> GetApps()
        {
            using var req = UnityWebRequest.Get("https://api.hyplay.com/v1/apps");
            req.SetRequestHeader("x-authorization", accessToken);
            await req.SendWebRequest();
            
            return HyplayJSON.Deserialize<List<HyplayApp>>(req.downloadHandler.text);
        }

        public async void UpdateCurrent()
        {
            #if UNITY_2022_1_OR_NEWER
            using var req = UnityWebRequest.Post($"https://api.hyplay.com/v1/apps/{currentApp.id}", HyplayJSON.Serialize(currentApp), "application/json");
            #else
            using var req = UnityWebRequest.Post($"https://api.hyplay.com/v1/apps/{currentApp.id}", "");
            HyplayJSON.SetData(ref req, HyplayJSON.Serialize(currentApp));
            #endif
            req.method = "PATCH";
            
            req.SetRequestHeader("x-authorization", accessToken);
            await req.SendWebRequest();
            Debug.Log(req.downloadHandler.text);
        }

        public async Task<HyplayImageAsset> CreateAsset(byte[] data)
        {
            var body = new Dictionary<string, string>
            {
                { "fileBase64", System.Convert.ToBase64String(data) }
            };
            
            #if UNITY_2022_1_OR_NEWER
            using var req = UnityWebRequest.Post($"https://api.hyplay.com/v1/assets", HyplayJSON.Serialize(body), "application/json");
            #else
            using var req = UnityWebRequest.Post($"https://api.hyplay.com/v1/assets", "");
            HyplayJSON.SetData(ref req, HyplayJSON.Serialize(body));
            #endif
            req.SetRequestHeader("x-authorization", accessToken);
            await req.SendWebRequest();

            try
            {
                return HyplayJSON.Deserialize<HyplayImageAsset>(req.downloadHandler.text);
            }
            catch
            {
                Debug.LogError($"Error uploading image: {req.downloadHandler.text}. Please check your access token.");
                return null;
            }
        }
        #endif
    }
}

#if UNITY_EDITOR
[Serializable]
public class SessionCreateResponse
{
    public string accessToken;
}
#endif