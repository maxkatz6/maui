using System;
using System.Threading.Tasks;
using AppKit;
using AuthenticationServices;
using Foundation;
using Microsoft.Maui.ApplicationModel;

namespace Microsoft.Maui.Authentication
{
	partial class WebAuthenticatorImplementation : IWebAuthenticator, IPlatformWebAuthenticatorCallback
	{
		const int asWebAuthenticationSessionErrorCodeCanceledLogin = 1;
		const string asWebAuthenticationSessionErrorDomain = "com.apple.AuthenticationServices.WebAuthenticationSession";

		readonly CallBackHelper callbackHelper;

		TaskCompletionSource<WebAuthenticatorResult> tcsResponse;
		Uri redirectUri;

		ASWebAuthenticationSession was;

		internal WebAuthenticatorImplementation()
		{
			callbackHelper = new CallBackHelper(this);
			callbackHelper.Register();
		}

		public async Task<WebAuthenticatorResult> AuthenticateAsync(WebAuthenticatorOptions webAuthenticatorOptions)
		{
			var url = webAuthenticatorOptions?.Url;
			var callbackUrl = webAuthenticatorOptions?.CallbackUrl;

			if (!AppInfoImplementation.VerifyHasUrlScheme(callbackUrl.Scheme))
				throw new InvalidOperationException("You must register your URL Scheme handler in your app's Info.plist!");

			// Cancel any previous task that's still pending
			if (tcsResponse?.Task != null && !tcsResponse.Task.IsCompleted)
				tcsResponse.TrySetCanceled();

			tcsResponse = new TaskCompletionSource<WebAuthenticatorResult>();
			redirectUri = callbackUrl;
			var scheme = redirectUri.Scheme;

			if (OperatingSystem.IsMacOSVersionAtLeast(10, 15))
			{
				void AuthSessionCallback(NSUrl cbUrl, NSError error)
				{
					if (error == null)
						OpenUrlCallback(cbUrl);
					else if (error.Domain == asWebAuthenticationSessionErrorDomain && error.Code == asWebAuthenticationSessionErrorCodeCanceledLogin)
						tcsResponse.TrySetCanceled();
					else
						tcsResponse.TrySetException(new NSErrorException(error));

					was = null;
				}

				was = new ASWebAuthenticationSession(WebUtils.GetNativeUrl(url), scheme, AuthSessionCallback);

				using (was)
				{
					var ctx = new ContextProvider(PlatformUtils.GetCurrentWindow());
					was.PresentationContextProvider = ctx;
					was.PrefersEphemeralWebBrowserSession = webAuthenticatorOptions?.PrefersEphemeralWebBrowserSession ?? false;

					was.Start();
					return await tcsResponse.Task;
				}
			}

			var opened = NSWorkspace.SharedWorkspace.OpenUrl(url);
			if (!opened)
				tcsResponse.TrySetException(new Exception("Error opening Safari"));

			return await tcsResponse.Task;
		}

		public bool OpenUrlCallback(Uri uri)
		{
			// If we aren't waiting on a task, don't handle the url
			if (tcsResponse?.Task?.IsCompleted ?? true)
				return false;

			try
			{
				// If we can't handle the url, don't
				if (!WebUtils.CanHandleCallback(redirectUri, uri))
					return false;

				tcsResponse.TrySetResult(new WebAuthenticatorResult(uri));
				return true;
			}
			catch (Exception ex)
			{
				tcsResponse.TrySetException(ex);
				return false;
			}
		}

		class ContextProvider : NSObject, IASWebAuthenticationPresentationContextProviding
		{
			public ContextProvider(NSWindow window) =>
				Window = window;

			public NSWindow Window { get; }

			public NSWindow GetPresentationAnchor(ASWebAuthenticationSession session)
				=> Window;
		}

		class CallBackHelper : NSObject
		{
			readonly WebAuthenticatorImplementation _impl;

			public CallBackHelper(WebAuthenticatorImplementation impl)
			{
				_impl = impl;
			}
			
			public void Register()
			{
				NSAppleEventManager.SharedAppleEventManager.SetEventHandler(
					this,
					new ObjCRuntime.Selector("handleAppleEvent:withReplyEvent:"),
					AEEventClass.Internet,
					AEEventID.GetUrl);
			}

			[Export("handleAppleEvent:withReplyEvent:")]
			public void HandleAppleEvent(NSAppleEventDescriptor evt, NSAppleEventDescriptor replyEvt)
			{
				var url = evt.ParamDescriptorForKeyword(DirectObject).StringValue;
				var uri = new Uri(url);
				_impl.OpenUrlCallback(WebUtils.GetNativeUrl(uri));
			}

			static uint GetDescriptor(string s) =>
				(uint)(s[0] << 24 | s[1] << 16 | s[2] << 8 | s[3]);

			static uint DirectObject => GetDescriptor("----");
		}
	}
}
