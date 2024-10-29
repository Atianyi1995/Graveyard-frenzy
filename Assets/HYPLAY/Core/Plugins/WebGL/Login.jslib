mergeInto(LibraryManager.library, {
    location_href: function () {
        var str = window.top.location.href;
        var bufferSize = lengthBytesUTF8(str) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(str, buffer, bufferSize);
        return buffer;
    },
    
    InitLogin: function ()
    {
        console.log(window.location.href);

        const cookies = document.cookie.split(';');

        for (let i = 0; i < cookies.length; i++) {
            const cookie = cookies[i].trim();
            if (cookie.startsWith('authToken=')) {
                var found = cookie.substring('authToken='.length);
                console.log("found cookie " + found);
            }
        }
    },
    
    DoLoginRedirect: function (appIdPtr, expiryPtr) {
        var expiry = UTF8ToString(expiryPtr);
        var appId = UTF8ToString(appIdPtr);
    
        // Construct the full HYPLAY OAuth URL
        var redirectUri = window.location.href;
        var url = "https://hyplay.com/oauth/authorize/?appId=" + appId + "&chain=HYCHAIN&responseType=token" + expiry + "&redirectUri=" + redirectUri;
        window.top.location = url;
    }
});