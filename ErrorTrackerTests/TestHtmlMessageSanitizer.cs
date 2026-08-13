using ErrorTrackerServer;
using ErrorTrackerServer.Controllers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace ErrorTrackerTests
{
	/// <summary>
	/// Tests the sanitization applied to HTML event messages.  These tests do not need a database.
	/// </summary>
	[TestClass]
	public class TestHtmlMessageSanitizer
	{
		/// <summary>
		/// Points <see cref="Settings.data"/> at a fresh settings object so that these tests are not affected by
		/// (and do not affect) any settings file on the machine running them.
		/// </summary>
		[TestInitialize]
		public void SetUpSettings()
		{
			Settings.data = new Settings();
			Settings.data.htmlMessageImageOriginWhitelist = "";
			Settings.data.htmlMessageAllowDataUriImages = true;
		}

		#region Scripting
		[TestMethod]
		public void ScriptTagsAreRemovedAlongWithTheirContent()
		{
			string result = HtmlMessageSanitizer.Sanitize("before<script>alert('xss')</script>after");
			Assert.IsFalse(result.IndexOf("script", StringComparison.OrdinalIgnoreCase) > -1, result);
			Assert.IsFalse(result.Contains("alert"), result);
			Assert.IsTrue(result.Contains("before") && result.Contains("after"), result);
		}

		[TestMethod]
		public void EventHandlerAttributesAreRemoved()
		{
			string result = HtmlMessageSanitizer.Sanitize("<div onclick=\"alert(1)\" onerror=\"alert(2)\">text</div>");
			Assert.IsFalse(result.IndexOf("onclick", StringComparison.OrdinalIgnoreCase) > -1, result);
			Assert.IsFalse(result.IndexOf("onerror", StringComparison.OrdinalIgnoreCase) > -1, result);
			Assert.IsTrue(result.Contains("text"), result);
		}

		[TestMethod]
		public void JavascriptUrlsAreRemoved()
		{
			string result = HtmlMessageSanitizer.Sanitize("<a href=\"javascript:alert(1)\">click</a>");
			Assert.IsFalse(result.IndexOf("javascript", StringComparison.OrdinalIgnoreCase) > -1, result);
			Assert.IsTrue(result.Contains("click"), result);
		}

		[TestMethod]
		public void StyleTagsAreRemoved()
		{
			string result = HtmlMessageSanitizer.Sanitize("<style>body { background: url(https://evil.example.com/x.png); }</style>text");
			Assert.IsFalse(result.IndexOf("<style", StringComparison.OrdinalIgnoreCase) > -1, result);
			Assert.IsFalse(result.IndexOf("evil.example.com", StringComparison.OrdinalIgnoreCase) > -1, result);
		}
		#endregion

		#region Resource loading
		[TestMethod]
		public void EmbeddedContentTagsAreRemoved()
		{
			foreach (string html in new string[] {
				"<iframe src=\"https://evil.example.com/\"></iframe>",
				"<object data=\"https://evil.example.com/\"></object>",
				"<embed src=\"https://evil.example.com/\" />",
				"<video src=\"https://evil.example.com/v.mp4\"></video>",
				"<audio src=\"https://evil.example.com/a.mp3\"></audio>",
				"<link rel=\"stylesheet\" href=\"https://evil.example.com/x.css\" />",
				"<base href=\"https://evil.example.com/\" />",
				"<input type=\"text\" />",
				"<svg><use href=\"https://evil.example.com/x.svg\" /></svg>"
			})
			{
				string result = HtmlMessageSanitizer.Sanitize(html);
				Assert.IsFalse(result.IndexOf("evil.example.com", StringComparison.OrdinalIgnoreCase) > -1,
					"Sanitizing \"" + html + "\" produced \"" + result + "\"");
			}
		}

		[TestMethod]
		public void CssCannotReferenceAUrl()
		{
			// Both the CSS property whitelist and the disallowed-value regex should prevent this, and the origin is
			// whitelisted here to prove that neither is relying on the image whitelist.
			Settings.data.htmlMessageImageOriginWhitelist = "https://images\\.example\\.com";
			foreach (string html in new string[] {
				"<div style=\"background-image: url(https://images.example.com/x.png)\">text</div>",
				"<div style=\"background: url('https://images.example.com/x.png')\">text</div>",
				"<div style=\"list-style-image: url(https://images.example.com/x.png)\">text</div>",
				"<div style=\"border-image: url(https://images.example.com/x.png)\">text</div>",
				"<div style=\"cursor: url(https://images.example.com/x.png), auto\">text</div>"
			})
			{
				string result = HtmlMessageSanitizer.Sanitize(html);
				Assert.IsFalse(result.IndexOf("images.example.com", StringComparison.OrdinalIgnoreCase) > -1,
					"Sanitizing \"" + html + "\" produced \"" + result + "\"");
				Assert.IsTrue(result.Contains("text"), result);
			}
		}

		[TestMethod]
		public void SafeCssIsKept()
		{
			string result = HtmlMessageSanitizer.Sanitize("<span style=\"color: red; font-weight: bold\">text</span>");
			Assert.IsTrue(result.IndexOf("color", StringComparison.OrdinalIgnoreCase) > -1, result);
			Assert.IsTrue(result.IndexOf("font-weight", StringComparison.OrdinalIgnoreCase) > -1, result);
		}
		#endregion

		#region Image origin whitelist
		[TestMethod]
		public void ImagesAreBlockedWhenTheWhitelistIsEmpty()
		{
			string result = HtmlMessageSanitizer.Sanitize("<img src=\"https://images.example.com/x.png\" alt=\"a picture\" />");
			Assert.IsFalse(result.IndexOf("images.example.com", StringComparison.OrdinalIgnoreCase) > -1, result);
			Assert.IsTrue(result.Contains("blockedImage"), result);
			Assert.IsTrue(result.Contains("a picture"), result);
		}

		[TestMethod]
		public void ImagesFromAWhitelistedOriginAreKept()
		{
			Settings.data.htmlMessageImageOriginWhitelist = "https://images\\.example\\.com";
			string result = HtmlMessageSanitizer.Sanitize("<img src=\"https://images.example.com/dir/x.png?a=b\" />");
			Assert.IsTrue(result.Contains("https://images.example.com/dir/x.png?a=b"), result);
			Assert.IsFalse(result.Contains("blockedImage"), result);
		}

		[TestMethod]
		public void WhitelistEntriesMustMatchTheEntireOrigin()
		{
			Settings.data.htmlMessageImageOriginWhitelist = "https://example\\.com";
			foreach (string origin in new string[] {
				"https://example.com.evil.net",   // whitelist entry appears as a prefix of the host
				"https://notexample.com",         // whitelist entry appears as a suffix of the host
				"https://sub.example.com",        // subdomains are not implied
				"http://example.com",             // the scheme is part of the origin
				"https://example.com:8443"        // a non-default port is part of the origin
			})
			{
				string result = HtmlMessageSanitizer.Sanitize("<img src=\"" + origin + "/x.png\" />");
				Assert.IsTrue(result.Contains("blockedImage"), "\"" + origin + "\" should not have matched. Result: " + result);
			}

			string allowed = HtmlMessageSanitizer.Sanitize("<img src=\"https://example.com/x.png\" />");
			Assert.IsFalse(allowed.Contains("blockedImage"), allowed);
		}

		[TestMethod]
		public void WhitelistIsAListOfExpressionsAndSkipsEmptyLines()
		{
			Settings.data.htmlMessageImageOriginWhitelist = "\r\n  \r\nhttps://a\\.example\\.com\r\n\r\nhttps://b\\.example\\.com\r\n";
			Assert.IsFalse(HtmlMessageSanitizer.Sanitize("<img src=\"https://a.example.com/x.png\" />").Contains("blockedImage"));
			Assert.IsFalse(HtmlMessageSanitizer.Sanitize("<img src=\"https://b.example.com/x.png\" />").Contains("blockedImage"));
			Assert.IsTrue(HtmlMessageSanitizer.Sanitize("<img src=\"https://c.example.com/x.png\" />").Contains("blockedImage"));
		}

		[TestMethod]
		public void WhitelistExpressionsMayMatchMultipleOrigins()
		{
			Settings.data.htmlMessageImageOriginWhitelist = "https://([a-z0-9-]+\\.)*example\\.com";
			Assert.IsFalse(HtmlMessageSanitizer.Sanitize("<img src=\"https://example.com/x.png\" />").Contains("blockedImage"));
			Assert.IsFalse(HtmlMessageSanitizer.Sanitize("<img src=\"https://cdn.images.example.com/x.png\" />").Contains("blockedImage"));
			Assert.IsTrue(HtmlMessageSanitizer.Sanitize("<img src=\"https://example.net/x.png\" />").Contains("blockedImage"));
		}

		[TestMethod]
		public void AnInvalidWhitelistExpressionDoesNotBreakTheOthers()
		{
			Settings.data.htmlMessageImageOriginWhitelist = "https://a\\.example\\.com\r\n*** not a regex ***\r\nhttps://b\\.example\\.com";
			Assert.IsFalse(HtmlMessageSanitizer.Sanitize("<img src=\"https://a.example.com/x.png\" />").Contains("blockedImage"));
			Assert.IsFalse(HtmlMessageSanitizer.Sanitize("<img src=\"https://b.example.com/x.png\" />").Contains("blockedImage"));
			Assert.IsTrue(HtmlMessageSanitizer.Sanitize("<img src=\"https://c.example.com/x.png\" />").Contains("blockedImage"));
		}

		[TestMethod]
		public void RelativeAndNonHttpImageUrlsAreBlocked()
		{
			Settings.data.htmlMessageImageOriginWhitelist = ".*";
			foreach (string src in new string[] { "/Wallpaper", "x.png", "//images.example.com/x.png", "ftp://images.example.com/x.png", "file:///C:/x.png" })
			{
				string result = HtmlMessageSanitizer.Sanitize("<img src=\"" + src + "\" />");
				Assert.IsTrue(result.Contains("blockedImage"), "\"" + src + "\" should have been blocked. Result: " + result);
			}
		}
		#endregion

		#region Data URI images
		[TestMethod]
		public void DataUriRasterImagesAreAllowed()
		{
			// A 1x1 transparent GIF.
			string src = "data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7";
			string result = HtmlMessageSanitizer.Sanitize("<img src=\"" + src + "\" />");
			Assert.IsTrue(result.Contains(src), result);
			Assert.IsFalse(result.Contains("blockedImage"), result);
		}

		[TestMethod]
		public void DataUriImagesCanBeDisabled()
		{
			Settings.data.htmlMessageAllowDataUriImages = false;
			string result = HtmlMessageSanitizer.Sanitize("<img src=\"data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7\" />");
			Assert.IsTrue(result.Contains("blockedImage"), result);
		}

		[TestMethod]
		public void UnsafeDataUrisAreBlocked()
		{
			foreach (string src in new string[] {
				"data:image/svg+xml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciPjwvc3ZnPg==",
				"data:image/svg+xml,<svg xmlns='http://www.w3.org/2000/svg'><script>alert(1)</script></svg>",
				"data:text/html;base64,PHNjcmlwdD5hbGVydCgxKTwvc2NyaXB0Pg==",
				"data:application/javascript;base64,YWxlcnQoMSk="
			})
			{
				string result = HtmlMessageSanitizer.Sanitize("<img src=\"" + src + "\" />");
				Assert.IsTrue(result.Contains("blockedImage"), "\"" + src + "\" should have been blocked. Result: " + result);
			}
		}

		[TestMethod]
		public void DataUrisAreNotAllowedOutsideOfImages()
		{
			string result = HtmlMessageSanitizer.Sanitize("<a href=\"data:text/html;base64,PHNjcmlwdD5hbGVydCgxKTwvc2NyaXB0Pg==\">click</a>");
			Assert.IsFalse(result.IndexOf("data:", StringComparison.OrdinalIgnoreCase) > -1, result);
		}
		#endregion

		#region Links and general markup
		[TestMethod]
		public void LinksAreKeptAndHardened()
		{
			string result = HtmlMessageSanitizer.Sanitize("<a href=\"https://example.com/page\">click</a>");
			Assert.IsTrue(result.Contains("https://example.com/page"), result);
			Assert.IsTrue(result.Contains("target=\"_blank\""), result);
			Assert.IsTrue(result.Contains("noopener"), result);
		}

		[TestMethod]
		public void OrdinaryFormattingSurvives()
		{
			string html = "<p>A <b>bold</b> <i>italic</i> message.</p><ul><li>one</li><li>two</li></ul>"
				+ "<table><tr><th>Key</th><td>Value</td></tr></table><pre>line1\r\n\tline2</pre>";
			string result = HtmlMessageSanitizer.Sanitize(html);
			foreach (string expected in new string[] { "<b>bold</b>", "<i>italic</i>", "<li>one</li>", "<th>Key</th>", "<td>Value</td>", "<pre>" })
				Assert.IsTrue(result.Contains(expected), "Expected \"" + expected + "\" in: " + result);
			Assert.IsTrue(result.Contains("\tline2"), "Tabs should be preserved inside <pre>: " + result);
		}

		[TestMethod]
		public void ClassAndIdAttributesAreRemoved()
		{
			// Message content must not be able to borrow the web interface's own styling or collide with its element IDs.
			string result = HtmlMessageSanitizer.Sanitize("<div class=\"eventBody\" id=\"app\">text</div>");
			Assert.IsFalse(result.Contains("eventBody"), result);
			Assert.IsFalse(result.Contains("app"), result);
			Assert.IsTrue(result.Contains("text"), result);
		}

		[TestMethod]
		public void NullMessageIsHandled()
		{
			Assert.IsNull(HtmlMessageSanitizer.Sanitize(null));
			Assert.IsNull(HtmlMessageSanitizer.ToPlainText(null));
		}
		#endregion

		#region Plain text conversion
		[TestMethod]
		public void PlainTextStripsMarkupAndDecodesEntities()
		{
			Assert.AreEqual("Hello world", HtmlMessageSanitizer.ToPlainText("<p>Hello <b>world</b></p>"));
			Assert.AreEqual("a < b & c", HtmlMessageSanitizer.ToPlainText("a &lt; b &amp; c"));
			Assert.AreEqual("one two", HtmlMessageSanitizer.ToPlainText("<div>one</div>\r\n\r\n<div>two</div>"));
		}

		[TestMethod]
		public void PlainTextDropsScriptAndStyleContent()
		{
			Assert.AreEqual("visible", HtmlMessageSanitizer.ToPlainText("<script>var a = 1;</script>visible<style>p { color: red; }</style>"));
		}

		[TestMethod]
		public void PlainTextFallsBackWhenThereIsNoText()
		{
			Assert.AreEqual("[HTML message]", HtmlMessageSanitizer.ToPlainText("<img src=\"https://example.com/x.png\" />"));
			Assert.AreEqual("[HTML message]", HtmlMessageSanitizer.ToPlainText(""));
		}
		#endregion

		#region Settings editor
		[TestMethod]
		public void WhitelistSettingIsEditableAsATextArea()
		{
			GetSettingsResponse response = new GetSettingsResponse();

			FieldEditSpec whitelist = response.editSpec.Find(s => s.key == "htmlMessageImageOriginWhitelist");
			Assert.IsNotNull(whitelist, "The image origin whitelist is missing from the settings editor.");
			Assert.AreEqual("textarea", whitelist.inputType);
			Assert.IsTrue(whitelist.rows > 1, "A textarea should be given more than one row.");
			Assert.IsFalse(string.IsNullOrWhiteSpace(whitelist.helpHtml), "The image origin whitelist needs help text.");

			FieldEditSpec dataUris = response.editSpec.Find(s => s.key == "htmlMessageAllowDataUriImages");
			Assert.IsNotNull(dataUris, "The data URI image setting is missing from the settings editor.");
			Assert.AreEqual("checkbox", dataUris.inputType);

			// Ordinary string settings must not have been turned into textareas.
			Assert.AreEqual("text", response.editSpec.Find(s => s.key == "systemName").inputType);
		}
		#endregion
	}
}
