using UnityEngine;
using UnityEngine.Events;

namespace HYPLAY.Core.Runtime
{
    public class HyplaySignInButton : MonoBehaviour
    {
        [SerializeField] private UnityEvent<HyplayUser> SignedIn;
        
        public async void SignIn()
        {
            var res = await HyplayBridge.LoginAndGetUserAsync();
            if (res.Success)
                SignedIn?.Invoke(res.Data);
            else
                Debug.LogError($"Error signing in: {res.Error}");
            
        }

        public async void SignInGuest()
        {
            var res = await HyplayBridge.GuestLoginAndReturnUserAsync();
            if (res.Success)
                SignedIn?.Invoke(res.Data);
            else
                Debug.LogError($"Error signing in: {res.Error}");
            
        }
    }
}
