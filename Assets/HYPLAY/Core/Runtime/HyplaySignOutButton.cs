using UnityEngine;

namespace HYPLAY.Core.Runtime
{
    public class HyplaySignOutButton : MonoBehaviour
    {
        public async void DeleteSession()
        {
            await HyplayBridge.LogoutAsync();
        }
    }
}