﻿/**
 * Copyright (c) Crista Lopes (aka Diva). All rights reserved.
 * 
 * Redistribution and use in source and binary forms, with or without modification, 
 * are permitted provided that the following conditions are met:
 * 
 *     * Redistributions of source code must retain the above copyright notice, 
 *       this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright notice, 
 *       this list of conditions and the following disclaimer in the documentation 
 *       and/or other materials provided with the distribution.
 *     * Neither the name of the Organizations nor the names of Individual
 *       Contributors may be used to endorse or promote products derived from 
 *       this software without specific prior written permission.
 * 
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND 
 * ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES 
 * OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL 
 * THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, 
 * EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE 
 * GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED 
 * AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING 
 * NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED 
 * OF THE POSSIBILITY OF SUCH DAMAGE.
 * 
 */
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

using Nini.Config;
using log4net;
using OpenMetaverse;

using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Services.Interfaces;
using OpenSim.Services.AuthenticationService;
using OpenSim.Services.UserAccountService;
using OpenSim.Services.InventoryService;

using Diva.Wifi.WifiScript;
using Environment = Diva.Wifi.Environment;

namespace Diva.Wifi
{
    public class WebApp : IWebApp
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private string m_DocsPath = System.IO.Path.Combine("..", "WifiPages");
        public string DocsPath
        {
            get { return m_DocsPath; }
        }

        #region IWebApp variables accessible to the WifiScript engine

        private bool m_Installed = false;
        public bool IsInstalled
        {
            get { return m_Installed; }
            set { m_Installed = value; }
        }

        private int m_Port;
        public int Port
        {
            get { return m_Port; }
        }

        private string m_GridName;
        public string GridName
        {
            get { return m_GridName; }
        }
        private string m_LoginURL;
        public string LoginURL
        {
            get { return m_LoginURL; }
        }
        private string m_WebAddress;
        public string WebAddress
        {
            get { return m_WebAddress; }
        }

        private string m_AdminFirst;
        public string AdminFirst
        {
            get { return m_AdminFirst; }
        }
        private string m_AdminLast;
        public string AdminLast
        {
            get { return m_AdminLast; }
        }
        private string m_AdminEmail;
        public string AdminEmail
        {
            get { return m_AdminEmail; }
        }

        #endregion

        private IUserAccountService m_UserAccountService;
        private IAuthenticationService m_AuthenticationService;
        private IInventoryService m_InventoryService;

        // Sessions
        private Dictionary<string, SessionInfo> m_Sessions = new Dictionary<string, SessionInfo>();

        public WebApp(IConfigSource config, string configName, IHttpServer server)
        {
            m_log.Debug("[WebApp]: Starting...");

            ReadConfigs(config, configName);

            // Create the necessary services
            m_UserAccountService = new UserAccountService(config);
            m_AuthenticationService = new PasswordAuthenticationService(config);
            m_InventoryService = new InventoryService(config);

            // Create the "God" account if it doesn't exist
            CreateGod();

            // Register all the handlers
            server.AddStreamHandler(new WifiGetHandler(this));
            server.AddStreamHandler(new WifiInstallGetHandler(this));
            server.AddStreamHandler(new WifiInstallPostHandler(this));
            server.AddStreamHandler(new WifiLoginHandler(this));
            server.AddStreamHandler(new WifiLogoutHandler(this));
            server.AddStreamHandler(new WifiUserAccountGetHandler(this));
            server.AddStreamHandler(new WifiUserAccountPostHandler(this));
            server.AddStreamHandler(new WifiNewAccountGetHandler(this));
            server.AddStreamHandler(new WifiNewAccountPostHandler(this));
            server.AddStreamHandler(new WifiUserManagementGetHandler(this));
            server.AddStreamHandler(new WifiUserManagementPostHandler(this));

        }

        public void ReadConfigs(IConfigSource config, string configName)
        {
            // Read config vars
            IConfig appConfig = config.Configs[configName];
            m_GridName = appConfig.GetString("GridName", "My World");
            m_LoginURL = appConfig.GetString("LoginURL", "http://localhost:9000");
            m_WebAddress = appConfig.GetString("WebAddress", "http://localhost:8080");

            m_AdminFirst = appConfig.GetString("AdminFirst", string.Empty);
            m_AdminLast = appConfig.GetString("AdminLast", string.Empty);
            m_AdminEmail = appConfig.GetString("AdminEmail", string.Empty);

            if (m_AdminFirst == string.Empty || m_AdminLast == string.Empty || m_AdminEmail == string.Empty)
                // Can't proceed
                throw new Exception("Can't proceed. Please specify the administrator account in Wifi.ini");

            IConfig serverConfig = config.Configs["Network"];
            if (serverConfig != null)
                m_Port = Int32.Parse(serverConfig.GetString("port", "80"));

            m_log.DebugFormat("[Environment]: Initialized. Admin account is {0} {1}", m_AdminFirst, m_AdminLast);
        }
        private void CreateGod()
        {
            UserAccount god = m_UserAccountService.GetUserAccount(UUID.Zero, AdminFirst, AdminLast);
            if (god == null)
            {
                m_log.DebugFormat("[WebApp]: Administrator account {0} {1} does not exist. Creating it...", AdminFirst, AdminLast);
                // Doesn't exist. Create one
                god = new UserAccount(UUID.Zero, AdminFirst, AdminLast, AdminEmail);
                god.UserLevel = 500;
                god.UserTitle = "Administrator";
                god.UserFlags = 0;
                SetServiceURLs(god);
                m_UserAccountService.StoreUserAccount(god);
                m_InventoryService.CreateUserInventory(god.PrincipalID);
                // Signal that the App needs installation
                IsInstalled = false;
            }
            else
            {
                m_log.DebugFormat("[WebApp]: Administrator account {0} {1} exists.", AdminFirst, AdminLast);
                // Signal that the App has been previously installed
                IsInstalled = true;
            }

            if (god.UserLevel < 200)
            {
                // Might have existed but had wrong UserLevel
                god.UserLevel = 500;
                m_UserAccountService.StoreUserAccount(god);
            }

        }

        #region IWebApp

        public string InstallGetRequest(Environment env)
        {
            env.Flags = StateFlags.InstallForm;
            return ReadFile(env, "index.html");
        }

        public string InstallPostRequest(Environment env, string password, string password2)
        {
            m_log.DebugFormat("[WebApp]: UserAccountPostRequest");
            Request request = env.Request;

            if (password == password2)
            {
                UserAccount god = m_UserAccountService.GetUserAccount(UUID.Zero, AdminFirst, AdminLast);
                if (god != null)
                {
                    m_AuthenticationService.SetPassword(god.PrincipalID, password);
                    // And this finishes the installation procedure
                    IsInstalled = true;
                    env.Flags = StateFlags.InstallFormResponse;
                }

            }

            return ReadFile(env, "index.html");
        }

        public string LoginRequest(Environment env, string first, string last, string password)
        {
            m_log.DebugFormat("[WebApp]: LoginRequest {0} {1}", first, last);
            Request request = env.Request;
            string encpass = Util.Md5Hash(password);

            UserAccount account = m_UserAccountService.GetUserAccount(UUID.Zero, first, last);
            if (account == null)
            {
                env.Flags = StateFlags.FailedLogin;
                return ReadFile(env, "index.html");
            }

            string authtoken = m_AuthenticationService.Authenticate(account.PrincipalID, encpass, 30);
            if (authtoken == string.Empty)
            {
                env.Flags = StateFlags.FailedLogin;
                return ReadFile(env, "index.html");
            }

            // Successful login
            SessionInfo sinfo;
            sinfo.IpAddress = request.IPEndPoint.Address.ToString();
            sinfo.Sid = authtoken;
            sinfo.Account = account;
            m_Sessions.Add(authtoken, sinfo);
            env.Request.Query["sid"] = authtoken;

            env.Flags = StateFlags.IsLoggedIn | StateFlags.SuccessfulLogin;
            return PadURLs(env, authtoken, ReadFile(env, "index.html"));
        }

        public string LogoutRequest(Environment env)
        {
            m_log.DebugFormat("[WebApp]: LogoutRequest");
            Request request = env.Request;

            SessionInfo sinfo;
            if (TryGetSessionInfo(request, out sinfo))
            {
                m_Sessions.Remove(sinfo.Sid);
                m_AuthenticationService.Release(sinfo.Account.PrincipalID, sinfo.Sid);
            }

            env.Flags = 0;
            return ReadFile(env, "index.html");
        }

        public string UserAccountGetRequest(Environment env)
        {
            m_log.DebugFormat("[WebApp]: UserAccountGetRequest");
            Request request = env.Request;

            SessionInfo sinfo;
            if (TryGetSessionInfo(request, out sinfo))
            {
                env.Flags = StateFlags.IsLoggedIn | StateFlags.UserAccountForm;
                return PadURLs(env, sinfo.Sid, ReadFile(env, "index.html"));
            }
            else
            {
                return ReadFile(env, "index.html");
            }
        }

        public string UserAccountPostRequest(Environment env, string email, string oldpassword, string newpassword, string newpassword2)
        {
            m_log.DebugFormat("[WebApp]: UserAccountPostRequest");
            Request request = env.Request;

            SessionInfo sinfo;
            if (TryGetSessionInfo(request, out sinfo))
            {
                bool updated = false;
                if (email != string.Empty && email.Contains("@") && sinfo.Account.Email != email)
                {
                    sinfo.Account.Email = email;
                    m_UserAccountService.StoreUserAccount(sinfo.Account);
                    updated = true;
                }

                string encpass = Util.Md5Hash(oldpassword);
                if ((newpassword != string.Empty) && (newpassword == newpassword2) &&
                    m_AuthenticationService.Authenticate(sinfo.Account.PrincipalID, encpass, 30) != string.Empty)
                {
                    m_AuthenticationService.SetPassword(sinfo.Account.PrincipalID, newpassword);
                    updated = true;
                }

                if (updated)
                {
                    env.Flags = StateFlags.IsLoggedIn | StateFlags.UserAccountFormResponse;
                    m_log.DebugFormat("[WebApp]: Updated account for user {0}", sinfo.Account.Name);
                    return PadURLs(env, sinfo.Sid, ReadFile(env, "index.html"));
                }

                // nothing was updated, really
                env.Flags = StateFlags.IsLoggedIn | StateFlags.UserAccountForm;
                return PadURLs(env, sinfo.Sid, ReadFile(env, "index.html"));
            }
            else
            {
                m_log.DebugFormat("[WebApp]: Failed to get session info");
                return ReadFile(env, "index.html");
            }
        }

        public string NewAccountGetRequest(Environment env)
        {
            m_log.DebugFormat("[WebApp]: NewAccountGetRequest");
            Request request = env.Request;

            env.Flags = StateFlags.NewAccountForm;
            return ReadFile(env, "index.html");
        }

        public string NewAccountPostRequest(Environment env, string first, string last, string email, string password, string password2)
        {
            m_log.DebugFormat("[WebApp]: NewAccountPostRequest");
            Request request = env.Request;

            if ((password != string.Empty) && (password == password2))
            {
                UserAccount account = m_UserAccountService.GetUserAccount(UUID.Zero, first, last);
                if (account == null)
                {
                    // Create the account
                    account = new UserAccount(UUID.Zero, first, last, email);
                    SetServiceURLs(account);
                    m_UserAccountService.StoreUserAccount(account);

                    // Create the inventory
                    m_InventoryService.CreateUserInventory(account.PrincipalID);

                    // Store the password
                    m_AuthenticationService.SetPassword(account.PrincipalID, password);

                    env.Flags = StateFlags.NewAccountFormResponse;
                    m_log.DebugFormat("[WebApp]: Created account for user {0}", account.Name);
                }
                else
                    m_log.DebugFormat("[WebApp]: Attempt at creating an account that already exists");
            }
            else
            {
                m_log.DebugFormat("[WebApp]: did not create account because of password problems");
                env.Flags = StateFlags.NewAccountForm;
            }

            return ReadFile(env, "index.html");

        }

        public string UserManagementGetRequest(Environment env)
        {
            m_log.DebugFormat("[WebApp]: UserManagementGetRequest");
            Request request = env.Request;

            SessionInfo sinfo;
            if (TryGetSessionInfo(request, out sinfo) && (sinfo.Account.UserLevel >= 200))
            {
                env.Flags = StateFlags.IsLoggedIn | StateFlags.IsAdmin | StateFlags.UserSearchForm;
                return PadURLs(env, sinfo.Sid, ReadFile(env, "index.html"));
            }
            else
            {
                return ReadFile(env, "index.html");
            }
        }

        public string UserSearchPostRequest(Environment env, string terms)
        {
            m_log.DebugFormat("[WebApp]: UserSearchPostRequest");
            Request request = env.Request;

            SessionInfo sinfo;
            if (TryGetSessionInfo(request, out sinfo) && (sinfo.Account.UserLevel >= 200) && (terms != string.Empty))
            {
                env.Flags = StateFlags.IsLoggedIn | StateFlags.IsAdmin | StateFlags.UserSearchFormResponse;
                env.Data = terms;
            }

            return PadURLs(env, sinfo.Sid, ReadFile(env, "index.html"));
        }
        #endregion

        public string GetContent(Environment env)
        {
            m_log.DebugFormat("[WebApp]: GetContent, flags {0}", env.Flags);

            //if (!Environment.IsInstalled)
            //    return "Welcome! Please install Wifi.";
            if ((env.Flags & StateFlags.InstallForm) != 0)
                return ReadFile(env, "installform.html");
            if ((env.Flags & StateFlags.InstallFormResponse) != 0)
                return "Your Wifi has been installed. The administrator account is " + AdminFirst + " " + AdminLast;

            if ((env.Flags & StateFlags.FailedLogin) != 0)
                return "Login failed";
            if ((env.Flags & StateFlags.SuccessfulLogin) != 0)
            {
                return "Welcome to " + GridName + "!";
            }

            if ((env.Flags & StateFlags.NewAccountForm) != 0)
                return ReadFile(env, "newaccountform.html");
            if ((env.Flags & StateFlags.NewAccountFormResponse) != 0)
                return "Your account has been created.";

            if ((env.Flags & StateFlags.IsLoggedIn) != 0)
            {
                if ((env.Flags & StateFlags.UserAccountForm) != 0)
                    return ReadFile(env, "useraccountform.html");
                if ((env.Flags & StateFlags.UserAccountFormResponse) != 0)
                    return "Your account has been updated.";
                if ((env.Flags & StateFlags.UserSearchForm) != 0)
                    return ReadFile(env, "usersearchform.html");
                if ((env.Flags & StateFlags.UserSearchFormResponse) != 0)
                    return GetUserList(env);
            }

            return string.Empty;
        }

        public string GetMainMenu(Environment env)
        {
            if (!IsInstalled)
                return ReadFile(env, "main-menu-install.html");

            SessionInfo sinfo;
            if (TryGetSessionInfo(env.Request, out sinfo))
            {
                if (sinfo.Account.UserLevel >= 200) // Admin
                    return ReadFile(env, "main-menu-admin.html");

                return ReadFile(env, "main-menu-users.html");
            }

            return ReadFile(env, "main-menu.html");
        }


        public string GetLoginLogout(Environment env)
        {
            if (!IsInstalled)
                return string.Empty;

            SessionInfo sinfo;
            if (TryGetSessionInfo(env.Request, out sinfo))
                return ReadFile(env, "logout.html");

            return ReadFile(env, "login.html");
        }

        public string GetUserName(Environment env)
        {
            SessionInfo sinfo;
            if (TryGetSessionInfo(env.Request, out sinfo))
            {
                return sinfo.Account.FirstName + " " + sinfo.Account.LastName;
            }

            return "Who are you?";
        }

        public string GetUserEmail(Environment env)
        {
            SessionInfo sinfo;
            if (TryGetSessionInfo(env.Request, out sinfo))
            {
                if (sinfo.Account.Email == string.Empty)
                    return "No email on file";

                return sinfo.Account.Email ;
            }

            return "Who are you?";
        }

        public string GetUserImage(Environment env)
        {
            SessionInfo sinfo;
            if (TryGetSessionInfo(env.Request, out sinfo))
            {
                // TODO
                return "/wifi/images/temporaryphoto1.jpg";
            }

            // TODO
            return "/wifi/images/temporaryphoto1.jpg";
        }


        private bool TryGetSessionInfo(Request request, out SessionInfo sinfo)
        {
            bool success = false;
            sinfo = new SessionInfo();
            if (request.Query.ContainsKey("sid"))
            {
                string sid = request.Query["sid"].ToString();
                if (m_Sessions.ContainsKey(sid))
                {
                    if (m_Sessions[sid].IpAddress == request.IPEndPoint.Address.ToString())
                    {
                        sinfo = m_Sessions[sid];
                        success = true;
                    }
                }
            }

            return success;
        }

        // <a href="wifi/..." ...>
        static Regex href = new Regex("(<a\\s+href\\s*=\\s*\\\"(\\S+))\\\">");
        static Regex action = new Regex("(<form\\s+action\\s*=\\s*\\\"(\\S+))\\\".*>");

        private string PadURLs(Environment env, string sid, string html)
        {
            if ((env.Flags & StateFlags.IsLoggedIn) == 0)
                return html;

            // The user is logged in
            HashSet<string> uris = new HashSet<string>();
            MatchCollection matches_href = href.Matches(html);
            m_log.DebugFormat("[WebApp]: Matched uris {0}", matches_href.Count);
            MatchCollection matches_action = action.Matches(html);
            m_log.DebugFormat("[WebApp]: Matched uris {0}", matches_action.Count);
            foreach (Match match in matches_href)
            {
                // first group is always the total match
                if (match.Groups.Count > 2)
                {
                    string str = match.Groups[1].Value;
                    string uri = match.Groups[2].Value;
                    if (!uri.StartsWith("http") && !uri.EndsWith(".html") && !uri.EndsWith(".css"))
                        uris.Add(str);
                }
            }
            foreach (Match match in matches_action)
            {
                // first group is always the total match
                if (match.Groups.Count > 2)
                {
                    string str = match.Groups[1].Value;
                    string uri = match.Groups[2].Value;
                    if (!uri.StartsWith("http") && !uri.EndsWith(".html") && !uri.EndsWith(".css"))
                        uris.Add(str);
                }
            }

            
            foreach (string uri in uris)
            {
                m_log.DebugFormat("[WebApp]: replacing {0} with {1}", uri, uri + "?sid=" + sid);
                if (!uri.EndsWith("/"))
                    html = html.Replace(uri, uri + "/?sid=" + sid);
                else
                    html = html.Replace(uri, uri + "?sid=" + sid);
            }

            return html;
        }

        private void PrintStr(string html)
        {
            foreach (char c in html)
                Console.Write(c);
        }

        private string GetUserList(Environment env)
        {
            string retString = "No users found.";
            string terms = (string)env.Data;

            List<UserAccount> accounts = m_UserAccountService.GetUserAccounts(UUID.Zero, terms);
            if (accounts != null)
                m_log.DebugFormat("[WebApp]: GetUserList found {0} users in DB", accounts.Count);
            else
                m_log.DebugFormat("[WebApp]: GetUserList got null users from DB");

            if (accounts != null && accounts.Count > 0)
            {
                return ReadFile(env, "userlist.html", Objectify<UserAccount>(accounts));
            }

            return retString;
        }

        #region read html files

        private string ReadFile(Environment env, string path)
        {
            return ReadFile(env, path, null);
        }

        private string ReadFile(Environment env, string path, List<object> lot)
        {
            string file = Path.Combine(WifiUtils.DocsPath, path);
            try
            {
                using (StreamReader sr = new StreamReader(file))
                {
                    string content = sr.ReadToEnd();
                    Processor p = new Processor(this, env, lot);
                    return p.Process(content);
                }
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[WebApp]: Exception on ReadFile {0}: {1}", path, e);
                return string.Empty;
            }
        }

        #endregion

        #region Misc
        
        private void SetServiceURLs(UserAccount account)
        {
            account.ServiceURLs = new Dictionary<string, object>();
            account.ServiceURLs["HomeURI"] = LoginURL.ToString();
            account.ServiceURLs["InventoryServerURI"] = LoginURL.ToString();
            account.ServiceURLs["AssetServerURI"] = LoginURL.ToString();
        }

        private List<object> Objectify<T>(List<T> listOfThings)
        {
            List<object> listOfObjects = new List<object>();
            foreach (T thing in listOfThings)
                listOfObjects.Add(thing);

            return listOfObjects;                
        }

        #endregion Misc

    }

    struct SessionInfo
    {
        public string Sid;
        public string IpAddress;
        public UserAccount Account;
    }
}