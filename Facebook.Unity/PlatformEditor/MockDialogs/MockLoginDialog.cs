/**
 * Copyright (c) 2014-present, Facebook, Inc. All rights reserved.
 *
 * You are hereby granted a non-exclusive, worldwide, royalty-free license to use,
 * copy, modify, and distribute this software in source code or binary form for use
 * in connection with the web services and APIs provided by Facebook.
 *
 * As with any software that integrates with the Facebook platform, your use of
 * this software is subject to the Facebook Developer Principles and Policies
 * [http://developers.facebook.com/policy/]. This copyright notice shall be
 * included in all copies or substantial portions of the software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS
 * FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
 * COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER
 * IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
 * CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 */

using System.IO;
using System.Reflection;

namespace Facebook.Unity.Editor.Dialogs
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    // MODIFIED BY MICHAEL, FOR EASIER TOKEN USAGE.
    internal class MockLoginDialog : EditorFacebookMockDialog
    {
        private string accessToken = string.Empty;
        private string accessTokenPath = "token.txt";
        
        protected override string DialogTitle
        {
            get
            {
                return "Mock Login Dialog";
            }
        }

        private string ReadAccessTokenFromFile()
        {
            if (File.Exists(accessTokenPath))
            {
                string readText = File.ReadAllText(accessTokenPath);
                Debug.Log("ACCESS_TOKEN: " + readText);
                return readText;
            }
            return string.Empty;
        }

        protected override void DoGui()
        {
            string currPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            accessTokenPath = Path.Combine(currPath, accessTokenPath);
            Debug.Log("CURR_PATH: " + currPath);
            Debug.Log("TOKEN_PATH: " + accessTokenPath);
            
            if (File.Exists(accessTokenPath))
            {
                accessToken = ReadAccessTokenFromFile();
                Debug.Log("ACCESS_TOKEN_READ_ATTEMPT: " + accessToken);
                if (accessToken != null && !accessToken.Equals(string.Empty))
                {
                    Debug.Log("READ ACCESS_TOKEN FROM FILE!");
                    this.SendSuccessResult();
                    MonoBehaviour.Destroy(this);
                }
            }
           
            Debug.Log("NO ACCESS_TOKEN FILE FOUND, CREATING DIALOG!");
            
            GUILayout.BeginHorizontal();
            GUILayout.Label("User Access Token:");
            this.accessToken = GUILayout.TextField(this.accessToken, GUI.skin.textArea, GUILayout.MinWidth(400));
            GUILayout.EndHorizontal();
            GUILayout.Space(10);
            if (GUILayout.Button("Find Access Token"))
            {
                Application.OpenURL(string.Format("https://developers.facebook.com/tools/accesstoken/?app_id={0}", FB.AppId));
            }

            GUILayout.Space(20);
            
        }

        protected override void SendSuccessResult()
        {
            if (string.IsNullOrEmpty(this.accessToken))
            {
                this.SendErrorResult("Empty Access token string");
                return;
            }

            // Make a Graph API call to get FBID
            FB.API(
                "/me?fields=id&access_token=" + this.accessToken,
               HttpMethod.GET,
               delegate(IGraphResult graphResult)
            {
                if (!string.IsNullOrEmpty(graphResult.Error))
                {
                    this.SendErrorResult("Graph API error: " + graphResult.Error);
                    return;
                }

                string facebookID = graphResult.ResultDictionary["id"] as string;

                // Make a Graph API call to get Permissions
                FB.API(
                    "/me/permissions?access_token=" + this.accessToken,
                   HttpMethod.GET,
                   delegate(IGraphResult permResult)
                {
                    if (!string.IsNullOrEmpty(permResult.Error))
                    {
                        this.SendErrorResult("Graph API error: " + permResult.Error);
                        return;
                    }

                    // Parse permissions
                    List<string> grantedPerms = new List<string>();
                    List<string> declinedPerms = new List<string>();
                    var data = permResult.ResultDictionary["data"] as List<object>;
                    foreach (Dictionary<string, object> dict in data)
                    {
                        if (dict["status"] as string == "granted")
                        {
                            grantedPerms.Add(dict["permission"] as string);
                        }
                        else
                        {
                            declinedPerms.Add(dict["permission"] as string);
                        }
                    }

                    // Create Access Token
                    var newToken = new AccessToken(
                        this.accessToken,
                        facebookID,
                        DateTime.UtcNow.AddDays(60),
                        grantedPerms,
                        DateTime.UtcNow);

                    var result = (IDictionary<string, object>)MiniJSON.Json.Deserialize(newToken.ToJson());
                    result.Add("granted_permissions", grantedPerms);
                    result.Add("declined_permissions", declinedPerms);
                    if (!string.IsNullOrEmpty(this.CallbackID))
                    {
                        result[Constants.CallbackIdKey] = this.CallbackID;
                    }

                    if (this.Callback != null)
                    {
                        this.Callback(new ResultContainer(result));
                    }
                });
            });
        }
    }
}
