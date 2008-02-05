/*
* Copyright (c) Contributors, http://opensimulator.org/
* See CONTRIBUTORS.TXT for a full list of copyright holders.
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
*     * Redistributions of source code must retain the above copyright
*       notice, this list of conditions and the following disclaimer.
*     * Redistributions in binary form must reproduce the above copyright
*       notice, this list of conditions and the following disclaimer in the
*       documentation and/or other materials provided with the distribution.
*     * Neither the name of the OpenSim Project nor the
*       names of its contributors may be used to endorse or promote products
*       derived from this software without specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS AS IS AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/
using System;
using System.Collections.Generic;
using libsecondlife;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Console;
using OpenSim.Framework.Servers;

namespace OpenSim.Region.Communications.OGS1
{
    public class OGS1InventoryService : IInventoryServices
    {
        private string _inventoryServerUrl;
        private Dictionary<LLUUID, InventoryRequest> m_RequestingInventory = new Dictionary<LLUUID, InventoryRequest>();

        public OGS1InventoryService(string inventoryServerUrl)
        {
            _inventoryServerUrl = inventoryServerUrl;
        }

        #region IInventoryServices Members

        // See IInventoryServices
        public void RequestInventoryForUser(LLUUID userID, InventoryFolderInfo folderCallBack,
                                            InventoryItemInfo itemCallBack)
        {
            if (!m_RequestingInventory.ContainsKey(userID))
            {
                InventoryRequest request = new InventoryRequest(userID, folderCallBack, itemCallBack);
                m_RequestingInventory.Add(userID, request);
                RequestInventory(userID);
            }
        }

        /// <summary>
        /// Request the entire user's inventory (folders and items) from the inventory server.  
        /// 
        /// XXX May want to change this so that we don't end up shuffling over data which might prove
        /// entirely unnecessary.
        /// </summary>
        /// <param name="userID"></param>
        private void RequestInventory(LLUUID userID)
        {
            try
            {
                MainLog.Instance.Verbose(
                    "INVENTORY", "Requesting inventory from {0}/GetInventory/ for user {1}",
                    _inventoryServerUrl, userID);

                RestObjectPosterResponse<InventoryCollection> requester
                    = new RestObjectPosterResponse<InventoryCollection>();
                requester.ResponseCallback = InventoryResponse;

                requester.BeginPostObject<Guid>(_inventoryServerUrl + "/GetInventory/", userID.UUID);
            }
            catch (Exception e)
            {
                MainLog.Instance.Error("INVENTORY", e.ToString());
            }
        }

        /// <summary>
        /// Callback used by the inventory server GetInventory request
        /// </summary>
        /// <param name="userID"></param>        
        private void InventoryResponse(InventoryCollection response)
        {
            LLUUID userID = response.UserID;
            if (m_RequestingInventory.ContainsKey(userID))
            {
                MainLog.Instance.Verbose("INVENTORY",
                                         "Received inventory response for user {0} containing {1} folders and {2} items",
                                         userID, response.Folders.Count, response.AllItems.Count);

                InventoryFolderImpl rootFolder = null;
                InventoryRequest request = m_RequestingInventory[userID];
                foreach (InventoryFolderBase folder in response.Folders)
                {
                    if (folder.parentID == LLUUID.Zero)
                    {
                        InventoryFolderImpl newfolder = new InventoryFolderImpl(folder);
                        rootFolder = newfolder;
                        request.FolderCallBack(userID, newfolder);
                    }
                }

                if (rootFolder != null)
                {
                    foreach (InventoryFolderBase folder in response.Folders)
                    {
                        if (folder.folderID != rootFolder.folderID)
                        {
                            InventoryFolderImpl newfolder = new InventoryFolderImpl(folder);
                            request.FolderCallBack(userID, newfolder);
                        }
                    }

                    foreach (InventoryItemBase item in response.AllItems)
                    {
                        request.ItemCallBack(userID, item);
                    }
                }
                m_RequestingInventory.Remove(userID);
            }
            else
            {
                MainLog.Instance.Warn(
                    "INVENTORY",
                    "Received inventory response for {0} for which we do not have a record of requesting!",
                    userID);
            }
        }

        public void AddNewInventoryFolder(LLUUID userID, InventoryFolderBase folder)
        {
            SynchronousRestObjectPoster.BeginPostObject<InventoryFolderBase, bool>(
                "POST", _inventoryServerUrl + "/NewFolder/", folder);
        }

        public void MoveInventoryFolder(LLUUID userID, InventoryFolderBase folder)
        {
            SynchronousRestObjectPoster.BeginPostObject<InventoryFolderBase, bool>(
                "POST", _inventoryServerUrl + "/MoveFolder/", folder);
        }

        public void AddNewInventoryItem(LLUUID userID, InventoryItemBase item)
        {
            SynchronousRestObjectPoster.BeginPostObject<InventoryItemBase, bool>(
                "POST", _inventoryServerUrl + "/NewItem/", item);
        }

        public void DeleteInventoryItem(LLUUID userID, InventoryItemBase item)
        {
            SynchronousRestObjectPoster.BeginPostObject<InventoryItemBase, bool>(
                "POST", _inventoryServerUrl + "/DeleteItem/", item);
        }

        public bool HasInventoryForUser(LLUUID userID)
        {
            return false;
        }

        public InventoryFolderBase RequestRootFolder(LLUUID userID)
        {
            return null;
        }

        public void CreateNewUserInventory(LLUUID user)
        {
        }

        public List<InventoryFolderBase> RequestFirstLevelFolders(LLUUID userID)
        {
            return new List<InventoryFolderBase>();
        }

        #endregion

        public class InventoryRequest
        {
            public LLUUID UserID;
            public InventoryFolderInfo FolderCallBack;
            public InventoryItemInfo ItemCallBack;

            public InventoryRequest(LLUUID userId, InventoryFolderInfo folderCall, InventoryItemInfo itemCall)
            {
                UserID = userId;
                FolderCallBack = folderCall;
                ItemCallBack = itemCall;
            }
        }
    }
}