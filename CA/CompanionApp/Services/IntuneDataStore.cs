﻿using CompanionApp.Model;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace CompanionApp.Services
{
    class IntuneDataStore : IIntuneDataStore
    {
        HttpClient graphClient;

        public IntuneDataStore()
        {

        }

        public async Task<bool> UpdateDeviceAsync(Model.Device device)
        {
            var token = ADALAuthentication.Instance.AuthResult.AccessToken;
            graphClient = new HttpClient();
            graphClient.DefaultRequestHeaders.Add("Authorization", token);

            // Unassign the user if the UPN is empty
            if (device.UserPrincipalName == String.Empty)
            {
                string stringUnassignUserUrl = string.Format("https://graph.microsoft.com/beta/devicemanagement/windowsAutopilotDeviceIdentities/{0}/unassignUserFromDevice", device.ZtdId);
                var ret = await graphClient.PostAsync(
                    stringUnassignUserUrl,
                    new StringContent(String.Empty, Encoding.UTF8, "application/json"));

                if (ret.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    return await Task.FromResult(false);
                }
            }

            // Update the other fields
            string serializedItem;
            if (device.UserPrincipalName == String.Empty)
            {
                var data = new
                {
                    groupTag = device.GroupTag,
                    displayName = device.DeviceName
                };
                serializedItem = JsonConvert.SerializeObject(data);
            }
            else
            { 
                var data = new
                {
                    userPrincipalName = device.UserPrincipalName,
                    addressableUserName = device.AddressableUserName,
                    groupTag = device.GroupTag,
                    displayName = device.DeviceName
                };
                serializedItem = JsonConvert.SerializeObject(data);
            }

            string stringUpdateDeviceUrl = string.Format("https://graph.microsoft.com/beta/devicemanagement/windowsAutopilotDeviceIdentities/{0}/UpdateDeviceProperties", device.ZtdId);
            var result = await graphClient.PostAsync(
                stringUpdateDeviceUrl,
                new StringContent(serializedItem, Encoding.UTF8, "application/json"));

            if (result.StatusCode != System.Net.HttpStatusCode.OK)
            {
                return await Task.FromResult(false);
            }

            return await Task.FromResult(true);
        }

        public async Task<IEnumerable<User>> ListAllUsersAsync()
        {
            List<User> users = new List<User>();
            var token = ADALAuthentication.Instance.AuthResult.AccessToken;
            graphClient = new HttpClient();
            graphClient.DefaultRequestHeaders.Add("Authorization", token);

            //var result = await graphClient.GetStringAsync($"users");
            var result = await graphClient.GetStringAsync("https://graph.microsoft.com/beta/users");

            JToken jtokenResult = JsonConvert.DeserializeObject<JToken>(result);
            JArray JsonValues = jtokenResult["value"] as JArray;

            foreach (var item in JsonValues)
            {
                User user = new User();
                user.DisplayName = item["displayName"].Value<string>();
                user.Surname = item["givenName"].Value<string>();
                user.UserPrincipalName = item["userPrincipalName"].Value<string>();
                users.Add(user);
            }

            return users;
        }

        public async Task<IEnumerable<User>> SearchUserAsync(string userName)
        {
            List<User> users = new List<User>();
            var token = ADALAuthentication.Instance.AuthResult.AccessToken;
            graphClient = new HttpClient();
            graphClient.DefaultRequestHeaders.Add("Authorization", token);

            //var result = await graphClient.GetStringAsync($"users");
            var result = await graphClient.GetStringAsync("https://graph.microsoft.com/beta/users?$filter=startswith(userPrincipalName,'"+userName+"')");

            JToken jtokenResult = JsonConvert.DeserializeObject<JToken>(result);
            JArray JsonValues = jtokenResult["value"] as JArray;

            foreach (var item in JsonValues)
            {
                User user = new User();
                user.DisplayName = item["displayName"].Value<string>();
                user.Surname = item["givenName"].Value<string>();
                user.UserPrincipalName = item["userPrincipalName"].Value<string>();
                users.Add(user);
            }

            return users;
            //return await Task.FromResult(users);
        }

        public async Task Sync()
        {
            var token = ADALAuthentication.Instance.AuthResult.AccessToken;
            graphClient = new HttpClient();
            graphClient.DefaultRequestHeaders.Add("Authorization", token);

            await graphClient.PostAsync("https://graph.microsoft.com/beta/deviceManagement/windowsAutopilotSettings/sync",
                                new StringContent("", Encoding.UTF8, "application/json"));
        }

        public async Task LogOutUser()
        {
            // DependencyService.Get<IADALAuthenticator>().PlatformParameters = null;
        }

        public async Task<Info> GetInfo()
        {
            Info i = new Info();

            var token = ADALAuthentication.Instance.AuthResult.AccessToken;
            graphClient = new HttpClient();
            graphClient.DefaultRequestHeaders.Add("Authorization", token);

            var result = await graphClient.GetStringAsync("https://graph.microsoft.com/v1.0/organization");

            JToken jtokenResult = JsonConvert.DeserializeObject<JToken>(result);
            JArray JsonValues = jtokenResult["value"] as JArray;

            foreach (var item in JsonValues)
            {
                i.TenantID = item["id"].Value<string>();
                i.TenantDisplayName = item["displayName"].Value<string>();
                JArray domains = item["verifiedDomains"] as JArray;
                foreach (var domain in domains)
                {
                    if (domain["isInitial"].Value<bool>())
                    {
                        i.TenantName = domain["name"].Value<string>();
                    }
                }
            }

            return await Task.FromResult(i);
        }

        public async Task<IEnumerable<Model.Device>> SearchDevicesBySerialAsync(string serial)
        {
            List<Model.Device> devices = new List<Model.Device>();
            var token = ADALAuthentication.Instance.AuthResult.AccessToken;
            graphClient = new HttpClient();
            graphClient.DefaultRequestHeaders.Add("Authorization", token);

            var result = await graphClient.GetStringAsync("https://graph.microsoft.com/beta/deviceManagement/windowsAutopilotDeviceIdentities?$filter=contains(serialNumber,'" + serial + "')");

            JToken jtokenResult = JsonConvert.DeserializeObject<JToken>(result);
            JArray JsonValues = jtokenResult["value"] as JArray;

            foreach (var item in JsonValues)
            {
                devices.Add(await ProcessDevice(item));
            }
            return devices;
        }

        public async Task<IEnumerable<Model.Device>> SearchDevicesByZtdIdAsync(string ztdId)
        {
            List<Model.Device> devices = new List<Model.Device>();
            var token = ADALAuthentication.Instance.AuthResult.AccessToken;
            graphClient = new HttpClient();
            graphClient.DefaultRequestHeaders.Add("Authorization", token);

            var result = await graphClient.GetStringAsync("https://graph.microsoft.com/beta/deviceManagement/windowsAutopilotDeviceIdentities/" + ztdId + "?$expand=deploymentProfile,intendedDeploymentProfile");

            JToken item = JsonConvert.DeserializeObject<JToken>(result);

            Model.Device device = await ProcessDevice(item);
            devices.Add(device);

            return devices;
        }

        private async Task<Model.Device> ProcessDevice(JToken item)
        {
            Model.Device device = new Model.Device();
            device.SerialNumber = item["serialNumber"].Value<string>();
            device.Manufacturer = item["manufacturer"].Value<string>();
            device.Model = item["model"].Value<string>();
            device.GroupTag = item["groupTag"].Value<string>();
            device.PurchaseOrderNumber = item["purchaseOrderIdentifier"].Value<string>();
            device.AddressableUserName = item["addressableUserName"].Value<string>();
            device.UserPrincipalName = item["userPrincipalName"].Value<string>();
            device.AzureActiveDirectoryDeviceId = item["azureActiveDirectoryDeviceId"].Value<string>();
            device.ManagedDeviceId = item["managedDeviceId"].Value<string>();
            device.ZtdId = item["id"].Value<string>();

            // Get details from Autopilot device
            var autopilotDetails = await graphClient.GetStringAsync("https://graph.microsoft.com/beta/deviceManagement/windowsAutopilotDeviceIdentities/" + device.ZtdId + "?$expand=deploymentProfile,intendedDeploymentProfile");
            JToken autopilotToken = JsonConvert.DeserializeObject<JToken>(autopilotDetails);
            device.DeviceName = autopilotToken["displayName"].Value<string>();
            if (autopilotToken["deploymentProfile"].HasValues)
            {
                device.DeploymentProfile = autopilotToken["deploymentProfile"]["displayName"].Value<string>();
            }

            // Get the AAD device details
            try
            {
                var aadDevice = await graphClient.GetStringAsync("https://graph.microsoft.com/v1.0/devices?$filter=deviceId eq '" + device.AzureActiveDirectoryDeviceId + "'");
                JToken aadDevices = JsonConvert.DeserializeObject<JToken>(aadDevice);
                JArray aadDeviceList = aadDevices["value"] as JArray;
                device.AzureActiveDirectoryDeviceName = aadDeviceList[0]["displayName"].Value<string>();
                device.AzureADId = aadDeviceList[0]["id"].Value<string>(); 
            }
            catch
            {
                device.AzureActiveDirectoryDeviceName = "";
            }

            // Get the Intune device details
            try
            {
                var intuneDevice = await graphClient.GetStringAsync("https://graph.microsoft.com/v1.0/deviceManagement/managedDevices/" + device.ManagedDeviceId);
                JToken intuneDeviceToken = JsonConvert.DeserializeObject<JToken>(intuneDevice);
                device.ManagedDeviceName = intuneDeviceToken["deviceName"].Value<string>();
            }
            catch
            {
                // Intune device not found
                device.ManagedDeviceName = "";
            }

            // find group membership
            try
            {
                var deviceGroup = await graphClient.GetStringAsync("https://graph.microsoft.com/v1.0/devices/" + device.AzureADId + "/memberOf");
                JToken deviceGroupToken = JsonConvert.DeserializeObject<JToken>(deviceGroup);
                
                device.Groups = new List<Group>();
                foreach (var grp in deviceGroupToken.Last.Children().Children())
                {
                    device.Groups.Add(new Group() { DisplayName = grp["displayName"].Value<string>(), Id = grp["id"].Value<string>() });
                }
            }
            catch
            {
                device.Groups = new List<Group>();
            }

            return device;
        }

        public async Task<IEnumerable<DeviceCategory>> ListAllCategoriesAsync()
        {
            List<DeviceCategory> categories = new List<DeviceCategory>();
            var token = ADALAuthentication.Instance.AuthResult.AccessToken;
            graphClient = new HttpClient();
            graphClient.DefaultRequestHeaders.Add("Authorization", token);

            var result = await graphClient.GetStringAsync("https://graph.microsoft.com/v1.0/deviceManagement/deviceCategories");

            JToken jtokenResult = JsonConvert.DeserializeObject<JToken>(result);
            JArray JsonValues = jtokenResult["value"] as JArray;

            // Add the "Unassigned" category
            categories.Add(new DeviceCategory() { Id = Guid.Empty.ToString(), DisplayName = "Unassigned" });
            foreach (var item in JsonValues)
            {
                DeviceCategory cat = new DeviceCategory();
                cat.DisplayName = item["displayName"].Value<string>();
                cat.Id = item["id"].Value<string>();
                categories.Add(cat);
            }

            return categories;
        }

        public async Task<bool> AssignCategory(Model.Device device)
        {
            // For whatever reason, only the beta namespace works for this, so don't change 
            // it unless you know the v1.0 namespace now works.

            var token = ADALAuthentication.Instance.AuthResult.AccessToken;
            graphClient = new HttpClient();
            graphClient.DefaultRequestHeaders.Add("Authorization", token);

            string stringDeviceUrl = string.Format("https://graph.microsoft.com/beta/deviceManagement/managedDevices('{0}')/deviceCategory/$ref", device.ManagedDeviceId);
            string categoryId = String.Empty;
            foreach (var category in device.CategoryList)
            {
                if (category.Id == device.ManagedDeviceCategoryId)
                {
                    categoryId = category.Id;
                }
            }

            string payload = string.Format("{{ \"@odata.id\": \"https://graph.microsoft.com/beta/deviceManagement/deviceCategories/{0}\"}}", categoryId);
            var result = await graphClient.PutAsync(
                stringDeviceUrl, 
                new StringContent(payload, Encoding.UTF8, "application/json"));

            if (result.StatusCode != System.Net.HttpStatusCode.NoContent)
            {
                return await Task.FromResult(false);
            }

            return await Task.FromResult(true);
        }

        public async Task<IEnumerable<Group>> ListAllGroupsAsync()
        {
            List<Group> groups = new List<Group>();
            var token = ADALAuthentication.Instance.AuthResult.AccessToken;
            graphClient = new HttpClient();
            graphClient.DefaultRequestHeaders.Add("Authorization", token);

            //var result = await graphClient.GetStringAsync($"users");
            var result = await graphClient.GetStringAsync("https://graph.microsoft.com/beta/groups");

            JToken jtokenResult = JsonConvert.DeserializeObject<JToken>(result);
            JArray JsonValues = jtokenResult["value"] as JArray;

            foreach (var item in JsonValues)
            {
                Group group = new Group();
                group.DisplayName = item["displayName"].Value<string>();
                group.Id = item["id"].Value<string>();
                groups.Add(group);
            }

            return groups;
        }

        public async Task<IEnumerable<Group>> SearchGroupAsync(string groupName)
        {
            List<Group> groups = new List<Group>();
            var token = ADALAuthentication.Instance.AuthResult.AccessToken;
            graphClient = new HttpClient();
            graphClient.DefaultRequestHeaders.Add("Authorization", token);

            //var result = await graphClient.GetStringAsync($"users");
            var result = await graphClient.GetStringAsync("https://graph.microsoft.com/beta/groups?$filter=startswith(displayName,'" + groupName + "')");

            JToken jtokenResult = JsonConvert.DeserializeObject<JToken>(result);
            JArray JsonValues = jtokenResult["value"] as JArray;

            foreach (var item in JsonValues)
            {
                Group group = new Group();
                group.DisplayName = item["displayName"].Value<string>();
                group.Id = item["id"].Value<string>();
                groups.Add(group);
            }

            return groups;
        }
    }
}

public static class HttpClientExtensions
{
    public static async Task<HttpResponseMessage> PatchAsync(this HttpClient client, string requestUri, HttpContent iContent)
    {
        var method = new HttpMethod("PATCH");
        var request = new HttpRequestMessage(method, new Uri(requestUri))
        {
            Content = iContent
        };

        HttpResponseMessage response = new HttpResponseMessage();
        response = await client.SendAsync(request);

        return response;
    }
}