﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http.Controllers;
using Microsoft.AspNet.WebHooks.Properties;
using Newtonsoft.Json.Linq;

namespace Microsoft.AspNet.WebHooks
{
    /// <summary>
    /// Provides an <see cref="IWebHookReceiver"/> implementation which supports WebHooks generated by Visual Studio Team Services. 
    /// 
    /// The corresponding WebHook URI is of the form '<c>https://&lt;host&gt;/api/webhooks/incoming/vsts/{id}?code={code}</c>'.
    /// For security reasons the WebHook URI must be an <c>https</c> URI and contain a 'code' query parameter with the
    /// same value as configured in the '<c>MS_WebHookReceiverSecret_Tfs</c>' application setting, optionally using IDs
    /// to differentiate between multiple WebHooks, for example '<c>secret0, id1=secret1, id2=secret2</c>'.
    /// The 'code' parameter must be between 32 and 128 characters long.
    /// 
    /// For details about Visual Studio Team Services WebHooks, see <c>https://www.visualstudio.com/en-us/get-started/integrate/service-hooks/webhooks-and-vso-vs</c>.
    /// </summary>
    public class VstsWebHookReceiver : WebHookReceiver
    {
        internal const string RecName = "vsts";
        internal const string EventTypeTokenName = "eventType";        

        /// <summary>
        /// Gets the receiver name for this receiver.
        /// </summary>
        public static string ReceiverName
        {
            get { return RecName; }
        }

        /// <inheritdoc />
        public override string Name
        {
            get { return RecName; }
        }

        /// <inheritdoc />
        public override async Task<HttpResponseMessage> ReceiveAsync(string id, HttpRequestContext context, HttpRequestMessage request)
        {
            if (id == null)
            {
                throw new ArgumentNullException(nameof(id));
            }
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (request.Method != HttpMethod.Post)
            {
                return CreateBadMethodResponse(request);
            }

            // Ensure that we use https and have a valid code parameter
            await EnsureValidCode(request, id);

            // Read the request entity body
            JObject jsonBody = await ReadAsJsonAsync(request);

            // Read the action from body
            JToken action;
            string actionAsString;
            if (!jsonBody.TryGetValue(EventTypeTokenName, out action))
            {
                request.GetConfiguration().DependencyResolver.GetLogger().Error(VstsReceiverResources.Receiver_NoEventType);
                return request.CreateErrorResponse(HttpStatusCode.BadRequest, VstsReceiverResources.Receiver_NoEventType);
            }
            else
            {
                actionAsString = action.Value<string>();
            }

            return await ExecuteWebHookAsync(id, context, request, new[] { actionAsString }, jsonBody);
        }
    }
}
