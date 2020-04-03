﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Microsoft.Owin;
using Microsoft.Owin.Infrastructure;
using Umbraco.Core;
using Umbraco.Core.Cache;
using Umbraco.Core.Configuration;
using Umbraco.Core.Hosting;
using Umbraco.Core.IO;
using Umbraco.Core.Security;

namespace Umbraco.Web.Security
{
    /// <summary>
    /// A custom cookie manager that is used to read the cookie from the request.
    /// </summary>
    /// <remarks>
    /// Umbraco's back office cookie needs to be read on two paths: /umbraco and /install and /base therefore we cannot just set the cookie path to be /umbraco,
    /// instead we'll specify our own cookie manager and return null if the request isn't for an acceptable path.
    /// </remarks>
    internal class BackOfficeCookieManager : ChunkingCookieManager, ICookieManager
    {
        private readonly IUmbracoContextAccessor _umbracoContextAccessor;
        private readonly IRuntimeState _runtime;
        private readonly IHostingEnvironment _hostingEnvironment;
        private readonly IGlobalSettings _globalSettings;
        private readonly IRequestCache _requestCache;
        private readonly string[] _explicitPaths;
        private readonly string _getRemainingSecondsPath;

        public BackOfficeCookieManager(IUmbracoContextAccessor umbracoContextAccessor, IRuntimeState runtime, IHostingEnvironment hostingEnvironment, IGlobalSettings globalSettings, IRequestCache requestCache)
            : this(umbracoContextAccessor, runtime, hostingEnvironment, globalSettings, requestCache, null)
        { }

        public BackOfficeCookieManager(IUmbracoContextAccessor umbracoContextAccessor, IRuntimeState runtime,  IHostingEnvironment hostingEnvironment, IGlobalSettings globalSettings, IRequestCache requestCache, IEnumerable<string> explicitPaths)
        {
            _umbracoContextAccessor = umbracoContextAccessor;
            _runtime = runtime;
            _hostingEnvironment = hostingEnvironment;
            _globalSettings = globalSettings;
            _requestCache = requestCache;
            _explicitPaths = explicitPaths?.ToArray();
            var backOfficePath = _globalSettings.GetBackOfficePath(_hostingEnvironment);
            _getRemainingSecondsPath = $"{backOfficePath}/backoffice/UmbracoApi/Authentication/GetRemainingTimeoutSeconds";
        }

        /// <summary>
        /// Explicitly implement this so that we filter the request
        /// </summary>
        /// <param name="context"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        string ICookieManager.GetRequestCookie(IOwinContext context, string key)
        {
            if (_umbracoContextAccessor.UmbracoContext == null || context.Request.Uri.IsClientSideRequest())
            {
                return null;
            }

            return ShouldAuthenticateRequest(
                context,
                _umbracoContextAccessor.UmbracoContext.OriginalRequestUrl) == false
                    //Don't auth request, don't return a cookie
                    ? null
                    //Return the default implementation
                    : GetRequestCookie(context, key);
        }

        /// <summary>
        /// Determines if we should authenticate the request
        /// </summary>
        /// <param name="owinContext"></param>
        /// <param name="originalRequestUrl"></param>
        /// <param name="checkForceAuthTokens"></param>
        /// <returns></returns>
        /// <remarks>
        /// We auth the request when:
        /// * it is a back office request
        /// * it is an installer request
        /// * it is a /base request
        /// * it is a preview request
        /// </remarks>
        internal bool ShouldAuthenticateRequest(IOwinContext owinContext, Uri originalRequestUrl, bool checkForceAuthTokens = true)
        {
            // Do not authenticate the request if we are not running (don't have a db, are not configured) - since we will never need
            // to know a current user in this scenario - we treat it as a new install. Without this we can have some issues
            // when people have older invalid cookies on the same domain since our user managers might attempt to lookup a user
            // and we don't even have a db.
            // was: app.IsConfigured == false (equiv to !Run) && dbContext.IsDbConfigured == false (equiv to Install)
            // so, we handle .Install here and NOT .Upgrade
            if (_runtime.Level == RuntimeLevel.Install)
                return false;

            var request = owinContext.Request;
            //check the explicit paths
            if (_explicitPaths != null)
            {
                return _explicitPaths.Any(x => x.InvariantEquals(request.Uri.AbsolutePath));
            }

            //check user seconds path
            if (request.Uri.AbsolutePath.InvariantEquals(_getRemainingSecondsPath)) return false;

            if (//check the explicit flag
                (checkForceAuthTokens && owinContext.Get<bool?>(Constants.Security.ForceReAuthFlag) != null)
                || (checkForceAuthTokens && _requestCache.IsAvailable && _requestCache.Get(Constants.Security.ForceReAuthFlag) != null)
                //check back office
                || request.Uri.IsBackOfficeRequest(_globalSettings, _hostingEnvironment)
                //check installer
                || request.Uri.IsInstallerRequest(_hostingEnvironment))
            {
                return true;
            }
            return false;
        }

    }
}
