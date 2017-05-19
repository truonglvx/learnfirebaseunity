using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Firebase.Auth;
using Firebase.Database;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System;
using Facebook.Unity;

public class Login : MonoBehaviour
{
    private Firebase.FirebaseApp fbApp;
    private FirebaseAuth auth;
    private FirebaseDatabase fbDB;
   // private DatabaseReference
    private FirebaseUser fbUser;
    public InputField UserNameInput, PasswordInput;
    
    public Button SignupButton, LoginButton;
    public Button FBLoginButton, FBLogoutButton;
    public Button SignupCustomTokenButton;
    public Text ErrorText;

    public Text DisplayName, UserId, Email, PhotoUrl;

    void Awake()
    {
        if (!FB.IsInitialized) 
        {
            // Initialize the Facebook SDK
            FB.Init(InitCallback, OnHideUnity);
        } 
        else 
        {
            // Already initialized, signal an app activation App Event
            FB.ActivateApp();
		}

    }

    void OnDestroy()
    {
        auth.StateChanged -= AuthStateChanged;
        auth = null;
    }

    void Start()
    {

       auth = FirebaseAuth.DefaultInstance;
       auth.StateChanged += AuthStateChanged;
       AuthStateChanged(this, null);
        //Just an example to save typing in the login form
        UserNameInput.text = "demofirebase@gmail.com";
        PasswordInput.text = "abcdefgh";

        SignupButton.onClick.AddListener(() => Signup(UserNameInput.text, PasswordInput.text));
        LoginButton.onClick.AddListener(() => LoginPassword(UserNameInput.text, PasswordInput.text));
        FBLoginButton.onClick.AddListener(() => loginFacebook());
        FBLogoutButton.onClick.AddListener(() => logoutFacebook());
        SignupCustomTokenButton.onClick.AddListener(() => SignupCustomToken());
    }

    void AuthStateChanged(object send, System.EventArgs eventArgs) {
        if(auth.CurrentUser != fbUser) {
            bool signedIn = fbUser != auth.CurrentUser && auth.CurrentUser != null;
            if(!signedIn && fbUser != null) {
                    UpdateErrorMessage("Signed Out: " + fbUser.UserId);
            }

            fbUser = auth.CurrentUser;
            if(signedIn) {
                UpdateErrorMessage("Signed In: " + fbUser.UserId);
                DisplayName.text = fbUser.DisplayName ?? "";
                UserId.text = fbUser.UserId;
                Email.text = fbUser.Email ?? "";
                System.Uri url = fbUser.PhotoUrl;
                string photoUrl = "";
                if(url != null)
                {
                    photoUrl = url.AbsolutePath;
                }
                PhotoUrl.text = photoUrl;
            }
        }
    }


//FaceBook[

    private void InitCallback ()
	{
		if (FB.IsInitialized) {
			// Signal an app activation App Event
			FB.ActivateApp();
			// Continue with Facebook SDK
			// ...
			} else {
			Debug.Log("Failed to Initialize the Facebook SDK");
		}
	}
	
	private void OnHideUnity (bool isGameShown)
	{
		if (!isGameShown) {
			// Pause the game - we will need to hide
			Time.timeScale = 0;
			} else {
			// Resume the game - we're getting focus again
			Time.timeScale = 1;
		}
	}

    public void loginFacebook()
    {
        List<string> perms = new List<string>(){"public_profile", "email", "user_friends"};
        FB.LogInWithReadPermissions(perms, AuthCallback);
    }
		
    public void logoutFacebook()
    {
        FB.LogOut ();
        auth.SignOut();
    }
    
    private void AuthCallback (ILoginResult result)
    {
        if (FB.IsLoggedIn) 
        {
            Debug.Log(result.ToString());

               // User.requestLogin (Facebook.Unity.AccessToken.CurrentAccessToken.UserId, m_loginWindows.loginResponse);
            var accessToken = AccessToken.CurrentAccessToken;
            Debug.Log("FaceBook Access Token: " + accessToken.TokenString);
         
            Credential credential = FacebookAuthProvider.GetCredential(accessToken.TokenString);

            auth.SignInWithCredentialAsync(credential).ContinueWith(task => {
                if (task.IsCanceled) {
                    Debug.LogError("FB SignInWithCredentialAsync was canceled.");
                    UpdateErrorMessage("FB SignInWithCredentialAsync was canceled.");
                    return;
                }
                if (task.IsFaulted) {
                    Debug.LogError("SignInWithCredentialAsync encountered an error: " + task.Exception);
                    return;
                }

                FirebaseUser newUser = task.Result;
                Debug.LogFormat("User signed in successfully: {0} ({1})",
                    newUser.DisplayName, newUser.UserId);
                DisplayName.text = newUser.DisplayName;
                UserId.text = newUser.UserId;

                UpdateErrorMessage("FB Signup Success");

                GetFireBaseToken();

            });
        } else {

                Debug.Log("User cancelled login");
                  UpdateErrorMessage("FB User cancelled login");
        }
    }


//FaceBook]




    public void Signup(string email, string password)
    {
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            //Error handling
            return;
        }

        auth.CreateUserWithEmailAndPasswordAsync(email, password).ContinueWith(task =>
        {
            if (task.IsCanceled)
            {
                Debug.LogError("CreateUserWithEmailAndPasswordAsync was canceled.");
                return;
            }
            if (task.IsFaulted)
            {
                Debug.LogError("CreateUserWithEmailAndPasswordAsync error: " + task.Exception);
                if (task.Exception.InnerExceptions.Count > 0)
                    UpdateErrorMessage(task.Exception.InnerExceptions[0].Message);
                return;
            }

            FirebaseUser newUser = task.Result; // Firebase user has been created.
            Debug.LogFormat("Firebase user created successfully: {0} ({1})",
                newUser.DisplayName, newUser.UserId);
            UpdateErrorMessage("FireBase Signup Success");
            GetFireBaseToken();
        });
    }

    private void UpdateErrorMessage(string message)
    {
        ErrorText.text = message;
        //Invoke("ClearErrorMessage", 3);
    }

    void ClearErrorMessage()
    {
        ErrorText.text = "";
    }
    public void LoginPassword(string email, string password)
    {
        auth.SignInWithEmailAndPasswordAsync(email, password).ContinueWith(task =>
        {
            if (task.IsCanceled)
            {
                Debug.LogError("SignInWithEmailAndPasswordAsync canceled.");
                return;
            }
            if (task.IsFaulted)
            {
                Debug.LogError("SignInWithEmailAndPasswordAsync error: " + task.Exception);
                if (task.Exception.InnerExceptions.Count > 0)
                    UpdateErrorMessage(task.Exception.InnerExceptions[0].Message);
                return;
            }

            FirebaseUser user = task.Result;
            Debug.LogFormat("User signed in successfully: {0} ({1})",
                user.DisplayName, user.UserId);

            UpdateErrorMessage("User signed in: " + user.UserId);
            PlayerPrefs.SetString("LoginUser", user != null ? user.UserId : "Unknown");
            //SceneManager.LoadScene("LoginResults");
           GetFireBaseToken();
        });
    }

    void GetFireBaseToken()
    {
        auth.CurrentUser.TokenAsync(true).ContinueWith(task => {
             if(task.IsCanceled) {
                 Debug.Log("TokenAsync was canceled");
                 return;
             }

             if(task.IsFaulted) {
                 Debug.Log("TokenAsync encountered an error " + task.Exception);
             }

                string token = task.Result;
                Debug.Log("Firebase Token:" + token);

                StartCoroutine(VerifyToken(token));
               
            });

    }

    IEnumerator VerifyToken(string token)
    {
        WWWForm httpForm = new WWWForm();
        httpForm.AddField("token", token);

        WWW http = new WWW("http://130.211.189.213:8000/createroom", httpForm);
        yield return http;
        if (!string.IsNullOrEmpty(http.error)) {
            ErrorText.text = http.error;
        }
        else {
            ErrorText.text = http.text;
            //ErrorText.text = "verify token successfully!";
        }
    }


    void SignupCustomToken()
    {
       
        StartCoroutine(CustomSignup());


    }

    IEnumerator CustomSignup()
    {
        WWW http = new WWW("http://130.211.189.213:8000/customtoken");
        // WWW http = new WWW("http://192.168.20.83:3000/customtoken");
         yield return http;
        if (!string.IsNullOrEmpty(http.error)) {
            ErrorText.text = http.error;
        }
        else {
            ErrorText.text = http.text;
            auth.SignInWithCustomTokenAsync(http.text).ContinueWith(task => {
                if(task.IsCanceled) {
                    return;
                }
                if(task.IsFaulted) {
                    return;
                }

                FirebaseUser user = task.Result;
                UserId.text = user.UserId;

                ErrorText.text = "signin custom token successfully!";
            });
            
        }
    }
}
