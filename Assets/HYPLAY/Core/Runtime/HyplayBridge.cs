using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Object = UnityEngine.Object;

namespace HYPLAY.Core.Runtime
{
    public class HyplayResponse<T> where T : class
    {
        public T Data;
        public string Error;
        public bool Success => string.IsNullOrWhiteSpace(Error);
    }
    public static class HyplayBridge
    {
        public static event Action LoggedIn;
        public static event Action LoggedOut;
        public static bool IsLoggedIn { get; private set; }
        
        private static HyplaySettings _settings;
        private static HyplayUser _currentUser;
        public static HyplayUser CurrentUser => _currentUser;
        
        private const string CachedTokenKey = "hyplay-token";

        public static HyplaySettings GetSettings()
        {
            if (_settings == null)
                _settings = Resources.Load<HyplaySettings>("Settings");

            return _settings;
        }
        
        [RuntimeInitializeOnLoadMethod]
        private static async void Init()
        {
            _currentUser = null;
            _settings = Resources.Load<HyplaySettings>("Settings");
            Application.deepLinkActivated += DeepLink;
            
            #if UNITY_WEBGL
            // on web, grab the token from the url if it exists
            DeepLink(Application.absoluteURL);
            // early return if we're logged in
            // we don't want to use the stored session if there's a user already signed in! 
            if (IsLoggedIn)
                return;
            #endif

            IsLoggedIn = false;
            var storedToken = PlayerPrefs.GetString(CachedTokenKey);
            if (!string.IsNullOrWhiteSpace(storedToken))
            {
                _settings.SetToken(storedToken);
                var getUser = await GetUserAsync(false);
                if (getUser.Success)
                {
                    _currentUser = getUser.Data;
                    IsLoggedIn = true;
                    LoggedIn?.Invoke();
                }
            }
        }

        public static async Task WaitForUserLoggedIn()
        {
            while (!IsLoggedIn)
                await Task.Yield();
        }

        public static string GetToken()
        {
            return _settings.Token;
        }

        internal static async void DeepLink(string obj)
        {
            if (!obj.Contains("token=")) return;
            var token = obj.Split("token=").Last();
            SetToken(token);

            var getUser = await GetUserAsync(false);
            if (getUser.Success)
                _currentUser = getUser.Data;
        }

        private static void SetToken(string token)
        {
            _settings.SetToken(token);
            IsLoggedIn = !string.IsNullOrWhiteSpace(token);
            if (IsLoggedIn)
            {
                PlayerPrefs.SetString(CachedTokenKey, token);
                LoggedIn?.Invoke();
            }
        }

        public static async void Login(Action onComplete)
        {
            await LoginAsync();
            onComplete?.Invoke();
        }

        public static async Task LoginAsync()
        {
            IsLoggedIn = false;
            _settings.DoLogin();
            _settings.SetToken("");
            while (string.IsNullOrWhiteSpace(_settings.Token))
                await Task.Yield();
        }

        public static async void GuestLoginAndReturnUser(Action<HyplayResponse<HyplayUser>> onComplete)
        {
            var res = await GuestLoginAndReturnUserAsync();
            onComplete(res);
        }
        
        public static async Task<HyplayResponse<HyplayUser>> GuestLoginAndReturnUserAsync()
        {
            var body = new Dictionary<string, string>
            {
                { "chain", "HYCHAIN" },
                { "appId", _settings.Current.id },
                { "isGuest", "true" },
                { "responseType", "token" }
            };
            using var req = UnityWebRequest.Post("https://api.hyplay.com/v1/sessions", 
                HyplayJSON.Serialize(body)
                ,"application/json");
            
            await req.SendWebRequest();

            switch (req.responseCode)
            {
                case 401:
                    _currentUser = null;
                    IsLoggedIn = false;
                    return new HyplayResponse<HyplayUser>
                    {
                        Data = null,
                        Error = "Not logged in"
                    };
                case 400:
                    _currentUser = null;
                    IsLoggedIn = false;
                    return new HyplayResponse<HyplayUser>
                    {
                        Data = null,
                        Error = req.downloadHandler.text
                    };
            }

            // there's no error, we're safe to continue
            var user = HyplayJSON.Deserialize<HyplayTokenRequest>(req.downloadHandler.text);
            
            _settings.SetToken(user.accessToken);
            PlayerPrefs.SetString(CachedTokenKey, user.accessToken);
            IsLoggedIn = true;
            LoggedIn?.Invoke();

            return await GetUserAsync(false);

        }

        public static async Task LogoutAsync()
        {
            var body = new Dictionary<string, string>
            {
                { "chain", "HYCHAIN" },
                { "appId", _settings.Current.id },
                { "userId", _currentUser.Id }
            };
            using var req = UnityWebRequest.Post("https://api.hyplay.com/v1/sessions", 
                HyplayJSON.Serialize(body)
                ,"application/json");
            
            req.method = UnityWebRequest.kHttpVerbDELETE;
            if (SetAuthHeader(req))
            {
                await req.SendWebRequest();
                
                _settings.SetToken(string.Empty);
                PlayerPrefs.SetString(CachedTokenKey, string.Empty);
                IsLoggedIn = false;
            }
            else
            {
                Debug.LogWarning("Tried signing out but there's no token!");
            }
            ClearUser();
        }

        public static async void Logout(Action onComplete)
        {
            await LogoutAsync();
            onComplete?.Invoke();
        }

        public static void ClearUser()
        {
            _currentUser = null;
            IsLoggedIn = false;
            LoggedOut?.Invoke();
        }
        
        public static bool SetAuthHeader(UnityWebRequest req)
        {
            if (string.IsNullOrWhiteSpace(_settings.Token))
            {
                Debug.LogError("Not logged in");
                return false;
            }
            
            req.SetRequestHeader("x-session-authorization", _settings.Token);
            return true;
        }
        
        public static async Task<HyplayResponse<HyplayUser>> LoginAndGetUserAsync()
        {
            await LoginAsync();
            return await GetUserAsync();
        }

        public static async void LoginAndGetUser(Action<HyplayResponse<HyplayUser>> onComplete)
        {
            var res = await LoginAndGetUserAsync();
            onComplete(res);
        }

        public static async Task GetUser(Action<HyplayResponse<HyplayUser>> onComplete)
        {
            var res = await GetUserAsync();
            onComplete(res);
        }

        public static async Task<HyplayResponse<HyplayUser>> GetUserAsync(bool useCache = true)
        {
            if (useCache && _currentUser != null)
            {
                return new HyplayResponse<HyplayUser>
                {
                    Data = _currentUser
                };
            }
            
            using var req = UnityWebRequest.Get("https://api.hyplay.com/v1/users/me");
            if (!SetAuthHeader(req))
            {
                ClearUser();
                return new HyplayResponse<HyplayUser>
                {
                    Data = null,
                    Error = "Not logged in"
                };
            }
            
            await req.SendWebRequest();

            if (req.responseCode == 401)
            {
                ClearUser();
                return new HyplayResponse<HyplayUser>
                {
                    Data = null,
                    Error = "Not logged in"
                };
            }

            var user = HyplayJSON.Deserialize<HyplayUser>(req.downloadHandler.text);
            var error = req.downloadHandler.error;

            _currentUser = user;
            return new HyplayResponse<HyplayUser>
            {
                Data = user,
                Error = error
            };
        }
        
        
        public static async Task<HyplayResponse<HyplayAppState<T>>> GetState<T>(string key)
        {
            using var req = UnityWebRequest.Get($"https://api.hyplay.com/v1/apps/{_settings.Current.id}/states?key={key}");
            if (!SetAuthHeader(req))
            {
                ClearUser();
                return new HyplayResponse<HyplayAppState<T>>
                {
                    Data = null,
                    Error = "Not logged in"
                };
            }
            
            await req.SendWebRequest();

            if (req.responseCode == 401)
            {
                ClearUser();
                return new HyplayResponse<HyplayAppState<T>>
                {
                    Data = null,
                    Error = "Not logged in"
                };
            }

            var res = HyplayJSON.Deserialize<HyplayAppState<T>>(req.downloadHandler.text);
            var error = req.downloadHandler.error;
            res.Key = key;
            return new HyplayResponse<HyplayAppState<T>>
            {
                Data = res,
                Error = error
            };
        }
        
        public static async Task<HyplayResponse<HyplayAppState<T>>> SetState<T>(HyplayAppState<T> state)
        {
             using var req = UnityWebRequest.Post($"https://api.hyplay.com/v1/apps/{_settings.Current.id}/states",
                HyplayJSON.Serialize(state)
                 ,"application/json");
            
            if (!SetAuthHeader(req))
            {
                ClearUser();
                return new HyplayResponse<HyplayAppState<T>>
                {
                    Data = null,
                    Error = "Not logged in"
                };
            }
            req.SetRequestHeader("x-app-authorization", _settings.Current.secretKey);
            
            await req.SendWebRequest();

            if (req.responseCode == 401)
            {
                ClearUser();
                return new HyplayResponse<HyplayAppState<T>>
                {
                    Data = null,
                    Error = "Not logged in"
                };
            }

            var res = HyplayJSON.Deserialize<HyplayAppState<T>>(req.downloadHandler.text);
            var error = req.downloadHandler.error;
            return new HyplayResponse<HyplayAppState<T>>
            {
                Data = res,
                Error = error
            };
        }

    }
}