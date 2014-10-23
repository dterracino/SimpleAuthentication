﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;
using SimpleAuthentication.Core;
using SimpleAuthentication.Core.Config;
using SimpleAuthentication.Core.Exceptions;
using SimpleAuthentication.Core.Tracing;

namespace SimpleAuthentication.Mvc
{
    public class SimpleAuthenticationController : AsyncController
    {
        private const string SessionKeyState = "SimpleAuthentication-StateKey-427B6ED7-A803-4F18-A396-0084417B548D";
        public const string DefaultRedirectRoute = "/authenticate/{providerName}";
        public const string DefaultCallbackRoute = "/authenticate/callback";

        private string _redirectRoute;
        private string _callbackRoute;
        private readonly WebApplicationService _webApplicationService;
        private readonly IAuthenticationProviderCallback _authenticationProviderCallback;

        private readonly Lazy<ITraceManager> _traceManager = new Lazy<ITraceManager>(() => new TraceManager());
        private string _returnToUrlParameterKey;

        public SimpleAuthenticationController(IAuthenticationProviderCallback authenticationProviderCallback,
            IConfigService configService
            //,
            //string redirectRoute = DefaultRedirectRoute,
            //string callbackRoute = DefaultCallbackRoute
            )
        {
            if (authenticationProviderCallback == null)
            {
                throw new ArgumentNullException("authenticationProviderCallback");
            }

            if (configService == null)
            {
                throw new ArgumentNullException("configService");
            }
            
            _authenticationProviderCallback = authenticationProviderCallback;

            //RedirectRoute = redirectRoute;
            //CallbackRoute = callbackRoute;

            _webApplicationService = new WebApplicationService(configService,
                TraceSource,
                CallbackRoute);
        }

        public string RedirectRoute
        {
            get
            {
                return (string.IsNullOrWhiteSpace(_redirectRoute))
                    ? DefaultRedirectRoute
                    : _redirectRoute;
            }
            private set
            {
                if (!string.IsNullOrWhiteSpace(value) &&
                    !value.Contains("{providerkey}"))
                {
                    var errorMessage = string.Format(
                        "The RedirectRoute requires a 'capture segment' with the Nancy route segment variable name '{{providerkey}}'. Eg. '{0}'",
                        DefaultRedirectRoute);
                    throw new ArgumentException(errorMessage, "value");
                }
                _redirectRoute = value;
            }
        }

        public string CallbackRoute
        {
            get
            {
                return (string.IsNullOrWhiteSpace(_callbackRoute))
                    ? DefaultCallbackRoute
                    : _callbackRoute;
            }
            set { _callbackRoute = value; }
        }

        public string ReturnToUrlParameterKey
        {
            get { return (string.IsNullOrEmpty(_returnToUrlParameterKey) ? "returnUrl" : _returnToUrlParameterKey); }
            set { _returnToUrlParameterKey = value; }
        }

        public ITraceManager TraceManager
        {
            get { return _traceManager.Value; }
        }

        private TraceSource TraceSource
        {
            get { return TraceManager["Nancy.SimpleAuthentication.SimpleAuthenticationModule"]; }
        }

        [HttpGet]
        public RedirectResult RedirectToProvider(string providerName)
        {
            if (string.IsNullOrEmpty(providerName))
            {
                var sampleUrl = RedirectRoute.Replace("{providerName}", "google");
                var errorMessage = string.Format(
                    "ProviderName value missing. You need to supply a valid provider name so we know where to redirect the user Eg. ....{0}",
                    sampleUrl);
                throw new AuthenticationException(errorMessage);
            }

            var returnUrl = Request.QueryString[ReturnToUrlParameterKey];
            var referer = Request.UrlReferrer != null
                ? Request.UrlReferrer.AbsoluteUri
                : null;

            var redirectToProviderData = new RedirectToProviderData(providerName,
                Request.Url,
                referer,
                returnUrl);

            var result = _webApplicationService.RedirectToProvider(redirectToProviderData);

            // Remember any important information for later, after we've returned back here.
            Session[SessionKeyState] = result.CacheData;

            // Now redirect :)
            return new RedirectResult(result.RedirectUrl.AbsoluteUri);
        }

        //public ActionResult AuthenticateCallbackResultAsync()
        //{
        //    TraceSource.TraceVerbose("Retrieving Cache values - State and RedirectToUrl.");

        //    var cacheData = Session[SessionKeyState] as CacheData;
        //    Session[SessionKeyState] = null;
        //    TraceSource.TraceInformation("Previous CacheData: {0}",
        //        cacheData == null
        //            ? "--no cache data"
        //            : cacheData.ToString());

        //    // If we don't have some previous state value cached, then it's possible we're trying to just
        //    // hack into the callback directly. ie. CSRF.
        //    if (cacheData == null ||
        //        string.IsNullOrWhiteSpace(cacheData.State))
        //    {
        //        throw new AuthenticationException(
        //            "No cache data or cached State value was found which generally means that a Cross Site Request Forgery attempt might be made. A 'State' value is generated by the server when a client prepares to rediect to an Authentication Provider and passes that generated state value to that Provider. The provider then passes that state value back, which proves that the client (ie. that's -you-) have actually authenticated against a provider. Otherwise, anyone can just hit the callback Url and impersonate another user, bypassing the authentication stage. So what's the solution: make sure you call the 'RedirectToProvider' endpoint *before* you hit the 'AuthenticateCallbackAsync' callback endpoint.");
        //    }

        //    var queryString = new Dictionary<string, string>();
        //    foreach (var key in Request.Query.Keys)
        //    {
        //        queryString.Add(key, Request.Query[key]);
        //    }

        //    var authenticateCallbackAsyncData = new AuthenticateCallbackAsyncData(Request.Url,
        //        cacheData,
        //        queryString);

        //    return
        //        await
        //            _webApplicationService
        //                .AuthenticateCallbackAsync<IAuthenticationProviderCallback, INancyModule, dynamic>(
        //                _authenticationProviderCallback,
        //                    this,
        //                    authenticateCallbackAsyncData);
        //}
    }
}
