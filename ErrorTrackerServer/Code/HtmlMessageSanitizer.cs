using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using BPUtil;
using Ganss.Xss;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace ErrorTrackerServer
{
	/// <summary>
	/// <para>Sanitizes HTML event messages so they can be safely rendered inside the Error Tracker web interface.</para>
	/// <para>The policy here is modeled on an email client: no scripting of any kind, and no resource may be loaded
	/// from the network except images whose origin matches the administrator-defined whitelist
	/// (<see cref="Settings.htmlMessageImageOriginWhitelist"/>).  Everything else that could cause the browser to
	/// make a request -- stylesheets, fonts, frames, media, CSS <c>url()</c> values -- is removed, so simply viewing
	/// an event can't reveal the viewer's IP address to a third party.</para>
	/// <para>Sanitization happens when an event is served, not when it is submitted, so that changes to these
	/// settings also affect events which were submitted previously.</para>
	/// </summary>
	public static class HtmlMessageSanitizer
	{
		#region Policy
		/// <summary>
		/// HTML tags which are allowed to survive sanitization.  Deliberately excluded: everything that can execute
		/// code or load a resource (script, style, link, iframe, frame, object, embed, applet, audio, video, source,
		/// track, canvas, svg, math, base, meta, form and its inputs).
		/// </summary>
		private static readonly string[] allowedTags = new string[] {
			"a", "abbr", "acronym", "address", "b", "bdi", "bdo", "big", "blockquote", "br", "caption", "center",
			"cite", "code", "col", "colgroup", "dd", "del", "details", "dfn", "div", "dl", "dt", "em", "figcaption",
			"figure", "font", "h1", "h2", "h3", "h4", "h5", "h6", "hr", "i", "img", "ins", "kbd", "li", "mark", "ol",
			"p", "pre", "q", "rp", "rt", "ruby", "s", "samp", "small", "span", "strike", "strong", "sub", "summary",
			"sup", "table", "tbody", "td", "tfoot", "th", "thead", "time", "tr", "tt", "u", "ul", "var", "wbr"
		};
		/// <summary>
		/// <para>HTML attributes which are allowed to survive sanitization.</para>
		/// <para>"class" and "id" are deliberately excluded so that event messages can neither borrow the web
		/// interface's own styling nor collide with element IDs used by the app.  "srcset", "usemap", "longdesc" and
		/// friends are excluded so that "src" is the only way an image can name a URL.</para>
		/// </summary>
		private static readonly string[] allowedAttributes = new string[] {
			"align", "alt", "bgcolor", "border", "cellpadding", "cellspacing", "color", "colspan", "datetime", "dir",
			"face", "height", "href", "hreflang", "lang", "nowrap", "reversed", "rowspan", "span", "src", "start",
			"style", "summary", "title", "valign", "width"
		};
		/// <summary>
		/// The only attributes which are treated as containing a URL.  Keeping this list minimal means the
		/// <see cref="Ganss.Xss.HtmlSanitizer.FilterUrl"/> handler can reason about an "img" element's URL being its
		/// "src" and nothing else.
		/// </summary>
		private static readonly string[] uriAttributes = new string[] { "href", "src" };
		/// <summary>
		/// URI schemes allowed in attributes.  "data" is not listed here on purpose; data URI images are handled as a
		/// special case in <see cref="IsImageUrlAllowed"/> so that they can only ever appear as an image source.
		/// </summary>
		private static readonly string[] allowedSchemes = new string[] { "http", "https", "mailto" };
		/// <summary>
		/// <para>CSS properties allowed in "style" attributes.</para>
		/// <para>Every property which can reference a URL is excluded (background, background-image, border-image,
		/// list-style, list-style-image, content, cursor, filter, mask, src), as is "position", which could be used to
		/// cover up parts of the web interface.</para>
		/// </summary>
		private static readonly string[] allowedCssProperties = new string[] {
			"background-color", "border", "border-bottom", "border-bottom-color", "border-bottom-left-radius",
			"border-bottom-right-radius", "border-bottom-style", "border-bottom-width", "border-collapse",
			"border-color", "border-left", "border-left-color", "border-left-style", "border-left-width",
			"border-radius", "border-right", "border-right-color", "border-right-style", "border-right-width",
			"border-spacing", "border-style", "border-top", "border-top-color", "border-top-left-radius",
			"border-top-right-radius", "border-top-style", "border-top-width", "border-width", "box-sizing",
			"caption-side", "clear", "color", "direction", "display", "empty-cells", "float", "font", "font-family",
			"font-size", "font-stretch", "font-style", "font-variant", "font-weight", "height", "letter-spacing",
			"line-height", "list-style-position", "list-style-type", "margin", "margin-bottom", "margin-left",
			"margin-right", "margin-top", "max-height", "max-width", "min-height", "min-width", "opacity", "overflow",
			"overflow-wrap", "overflow-x", "overflow-y", "padding", "padding-bottom", "padding-left", "padding-right",
			"padding-top", "table-layout", "text-align", "text-decoration", "text-indent", "text-overflow",
			"text-transform", "vertical-align", "white-space", "width", "word-break", "word-spacing", "word-wrap"
		};
		/// <summary>
		/// <para>A CSS property value matching this regex is discarded.</para>
		/// <para>This is a second line of defense behind <see cref="allowedCssProperties"/>: even if a URL-bearing
		/// property were somehow allowed, a value containing "url(" never survives, so CSS cannot make the browser
		/// fetch anything.  The "[&lt;&gt;]" part preserves HtmlSanitizer's own default behavior.</para>
		/// </summary>
		private static readonly Regex disallowedCssPropertyValue = new Regex(
			@"[<>]|url\s*\(|expression\s*\(|@import|-moz-binding|behaviou?r\s*:|(?:javascript|vbscript|data)\s*:",
			RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
		/// <summary>
		/// <para>Matches a base64-encoded data URI containing a raster image.</para>
		/// <para>SVG is excluded because an SVG document can carry script.  Browsers do not run script in an SVG that
		/// was loaded via "img", but there is no reason to depend on that.</para>
		/// </summary>
		private static readonly Regex dataUriImage = new Regex(
			@"^data:image/(?:png|jpeg|jpg|pjpeg|gif|bmp|webp|avif|x-icon|vnd\.microsoft\.icon)\s*;\s*base64\s*,[a-zA-Z0-9+/\s]*={0,2}$",
			RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
		/// <summary>
		/// Maximum time a single administrator-provided whitelist regex may spend matching one origin.
		/// </summary>
		private static readonly TimeSpan whitelistRegexTimeout = TimeSpan.FromMilliseconds(250);
		#endregion

		#region Sanitizing
		/// <summary>
		/// One configured sanitizer per thread.  HtmlSanitizer instances are not documented as thread-safe, and the
		/// configuration built in <see cref="CreateSanitizer"/> never changes -- the parts of the policy which the
		/// administrator can change are read from <see cref="Settings.data"/> each time a URL is filtered.
		/// </summary>
		[ThreadStatic]
		private static HtmlSanitizer _sanitizer;

		/// <summary>
		/// <para>Sanitizes an HTML event message, returning an HTML fragment which is safe to insert into the web
		/// interface.</para>
		/// <para>If sanitization fails for any reason, the message is HTML-encoded and returned as plain text, so a
		/// broken message can never end up rendered unsanitized.</para>
		/// </summary>
		/// <param name="html">HTML event message exactly as it was submitted.</param>
		/// <returns>A sanitized HTML fragment.  Returns null if [html] is null.</returns>
		public static string Sanitize(string html)
		{
			if (html == null)
				return null;
			try
			{
				if (_sanitizer == null)
					_sanitizer = CreateSanitizer();
				// No base URL is provided, so relative URLs are never resolved and never loaded.
				return _sanitizer.Sanitize(html);
			}
			catch (Exception ex)
			{
				Logger.Debug(ex, "Failed to sanitize an HTML event message. The message will be displayed as plain text.");
				return WebUtility.HtmlEncode(html);
			}
		}

		/// <summary>
		/// Builds a sanitizer configured with the policy defined at the top of this class.
		/// </summary>
		/// <returns></returns>
		private static HtmlSanitizer CreateSanitizer()
		{
			HtmlSanitizer sanitizer = new HtmlSanitizer();

			sanitizer.AllowedTags.Clear();
			sanitizer.AllowedTags.UnionWith(allowedTags);

			sanitizer.AllowedAttributes.Clear();
			sanitizer.AllowedAttributes.UnionWith(allowedAttributes);

			sanitizer.UriAttributes.Clear();
			sanitizer.UriAttributes.UnionWith(uriAttributes);

			sanitizer.UriListAttributes.Clear();

			sanitizer.AllowedSchemes.Clear();
			sanitizer.AllowedSchemes.UnionWith(allowedSchemes);

			sanitizer.AllowedCssProperties.Clear();
			sanitizer.AllowedCssProperties.UnionWith(allowedCssProperties);

			sanitizer.AllowedAtRules.Clear();

			// "data-*" attributes are harmless on their own but serve no purpose here.
			sanitizer.AllowDataAttributes = false;
			// Custom properties ("--foo") bypass the AllowedCssProperties list.
			sanitizer.AllowCssCustomProperties = false;
			sanitizer.DisallowCssPropertyValue = disallowedCssPropertyValue;
			// Content of a removed tag goes away with the tag. Notably this means <script>text</script> leaves nothing behind.
			sanitizer.KeepChildNodes = false;

			sanitizer.FilterUrl += (sender, e) =>
			{
				e.SanitizedUrl = FilterUrl(e.Tag, e.OriginalUrl, e.SanitizedUrl);
			};
			sanitizer.PostProcessDom += (sender, e) =>
			{
				PostProcessDom(e.Document);
			};

			return sanitizer;
		}

		/// <summary>
		/// <para>Decides the final value of a URL encountered during sanitization.</para>
		/// <para>An "img" element's URL is subject to the image origin whitelist.  Any other URL has already been
		/// screened against <see cref="allowedSchemes"/> by HtmlSanitizer, and is a URL the browser will only request
		/// if the user clicks it, so it is passed through unchanged.</para>
		/// </summary>
		/// <param name="tag">Element the URL belongs to.</param>
		/// <param name="originalUrl">URL as it appeared in the message.</param>
		/// <param name="sanitizedUrl">Value HtmlSanitizer arrived at, which is null when the scheme is not allowed.</param>
		/// <returns>The URL to use, or null to remove the attribute.</returns>
		private static string FilterUrl(IElement tag, string originalUrl, string sanitizedUrl)
		{
			if (tag != null && "IMG".Equals(tag.NodeName, StringComparison.OrdinalIgnoreCase))
			{
				if (IsImageUrlAllowed(originalUrl, out string allowedUrl))
					return allowedUrl;
				return null;
			}
			return sanitizedUrl;
		}

		/// <summary>
		/// <para>Returns true if the given image URL may be loaded by the browser.</para>
		/// <para>Only two kinds of URL are accepted: a base64 data URI containing a raster image (if data URI images
		/// are enabled), or an absolute http/https URL whose origin matches one of the administrator's whitelist
		/// regexes.  Relative URLs are rejected because there is nothing useful for them to point at.</para>
		/// </summary>
		/// <param name="url">URL as it appeared in the message.</param>
		/// <param name="allowedUrl">The URL to emit, which for a remote image is a normalized absolute URL so that the
		/// browser is guaranteed to request the same origin that was checked here.</param>
		/// <returns></returns>
		private static bool IsImageUrlAllowed(string url, out string allowedUrl)
		{
			allowedUrl = null;
			if (string.IsNullOrWhiteSpace(url))
				return false;

			string trimmed = url.Trim();

			if (trimmed.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
			{
				if (Settings.data.htmlMessageAllowDataUriImages && dataUriImage.IsMatch(trimmed))
				{
					allowedUrl = trimmed;
					return true;
				}
				return false;
			}

			if (!Uri.TryCreate(trimmed, UriKind.Absolute, out Uri uri))
				return false;
			if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
				return false;

			string origin = GetOrigin(uri);
			foreach (Regex rx in GetImageOriginWhitelist())
			{
				try
				{
					if (rx.IsMatch(origin))
					{
						allowedUrl = uri.AbsoluteUri;
						return true;
					}
				}
				catch (RegexMatchTimeoutException)
				{
					Logger.Debug("An image origin whitelist regex (" + rx + ") timed out while matching the origin \"" + origin + "\". The image was blocked.");
				}
			}
			return false;
		}

		/// <summary>
		/// Builds the origin string which whitelist regexes are matched against, e.g. "https://example.com" or
		/// "http://example.com:8080".  The port is included only when it is not the default port for the scheme.
		/// The host is in its ASCII (punycode) form so that an internationalized domain name always produces the
		/// same string.
		/// </summary>
		/// <param name="uri">An absolute http/https URI.</param>
		/// <returns></returns>
		private static string GetOrigin(Uri uri)
		{
			string origin = uri.Scheme + "://" + uri.IdnHost;
			if (!uri.IsDefaultPort)
				origin += ":" + uri.Port;
			return origin.ToLowerInvariant();
		}

		/// <summary>
		/// Runs after HtmlSanitizer is finished, to apply the finishing touches which can't be expressed as a
		/// tag/attribute policy.  Nothing added here is sanitized afterwards, so it must not be derived from
		/// unsanitized input.
		/// </summary>
		/// <param name="document">The sanitized document.</param>
		private static void PostProcessDom(IHtmlDocument document)
		{
			// An image whose source was blocked or was never usable is left with no "src". Rather than leaving a
			// broken image icon, swap in a placeholder that says what happened.
			foreach (IElement img in document.QuerySelectorAll("img").ToList())
			{
				if (!string.IsNullOrWhiteSpace(img.GetAttribute("src")))
					continue;
				IElement placeholder = document.CreateElement("span");
				placeholder.SetAttribute("class", "blockedImage");
				string alt = img.GetAttribute("alt");
				placeholder.TextContent = string.IsNullOrWhiteSpace(alt) ? "[image blocked]" : "[image blocked: " + alt.Trim() + "]";
				img.Parent?.ReplaceChild(placeholder, img);
			}

			// Links open in a new tab and don't hand the opened page a reference back to the web interface.
			foreach (IElement a in document.QuerySelectorAll("a[href]").ToList())
			{
				a.SetAttribute("target", "_blank");
				a.SetAttribute("rel", "noopener noreferrer nofollow");
			}
		}
		#endregion

		#region Image origin whitelist
		private static readonly object whitelistLock = new object();
		private static string whitelistSource = null;
		private static Regex[] whitelistRegexes = new Regex[0];

		/// <summary>
		/// Gets the compiled image origin whitelist, recompiling it if the setting has changed since last time.
		/// Lines which are empty or which fail to compile are skipped.
		/// </summary>
		/// <returns></returns>
		private static Regex[] GetImageOriginWhitelist()
		{
			string source = Settings.data.htmlMessageImageOriginWhitelist ?? "";
			lock (whitelistLock)
			{
				if (source == whitelistSource)
					return whitelistRegexes;

				List<Regex> regexes = new List<Regex>();
				foreach (string line in source.Split('\n'))
				{
					string pattern = line.Trim();
					if (pattern.Length == 0)
						continue;
					try
					{
						// The pattern is anchored so that a whitelist entry like "https://example\.com" can't be
						// satisfied by an origin which merely contains it, such as "https://example.com.evil.net".
						regexes.Add(new Regex("^(?:" + pattern + ")$",
							RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
							whitelistRegexTimeout));
					}
					catch (Exception ex)
					{
						Logger.Debug(ex, "Ignoring invalid image origin whitelist pattern: " + pattern);
					}
				}

				whitelistRegexes = regexes.ToArray();
				whitelistSource = source;
				return whitelistRegexes;
			}
		}
		#endregion

		#region Plain text conversion
		private static readonly Regex scriptOrStyleBlock = new Regex(@"<(script|style)\b[^>]*>.*?</\1\s*>",
			RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant | RegexOptions.Compiled);
		private static readonly Regex htmlTag = new Regex(@"<[^>]*>",
			RegexOptions.Singleline | RegexOptions.CultureInvariant | RegexOptions.Compiled);
		private static readonly Regex whitespaceRun = new Regex(@"\s+",
			RegexOptions.CultureInvariant | RegexOptions.Compiled);
		private static readonly Regex lineBreakingTag = new Regex(@"<(?:br|hr)\b[^>]*>|</?(?:p|div|pre|tr|li|ul|ol|table|blockquote|h[1-6]|dt|dd|dl|section|article|header|footer)\b[^>]*>",
			RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
		private static readonly Regex horizontalWhitespaceRun = new Regex(@"[^\S\n]+",
			RegexOptions.CultureInvariant | RegexOptions.Compiled);
		private static readonly Regex spaceAroundNewline = new Regex(@" ?\n ?",
			RegexOptions.CultureInvariant | RegexOptions.Compiled);
		private static readonly Regex excessNewlines = new Regex(@"\n{3,}",
			RegexOptions.CultureInvariant | RegexOptions.Compiled);
		/// <summary>
		/// Amount of an HTML message that is examined when producing plain text.  Plain text is only used for short
		/// previews and window titles, and this method runs once per event in a list which can hold thousands of
		/// them, so there is no reason to process an entire large message.
		/// </summary>
		private const int plainTextInputLimit = 8192;

		/// <summary>
		/// <para>Produces an approximate plain-text rendition of an HTML event message, for the places where markup
		/// can't be rendered: event list previews, the title bar of the event details pane, and so on.</para>
		/// <para>This strips markup rather than rendering it, so the result is not a faithful conversion.  It is only
		/// ever used as text, never as HTML, so imperfect output is a cosmetic issue and not a security one.</para>
		/// </summary>
		/// <param name="html">HTML event message.</param>
		/// <returns>Plain text.  Returns null if [html] is null.</returns>
		public static string ToPlainText(string html)
		{
			return ToPlainText(html, plainTextInputLimit);
		}

		private static readonly Regex astralNumericCharacterReference = new Regex(@"&#(?:[xX](?<hex>[0-9a-fA-F]{5,8})|(?<dec>[0-9]{5,10}));",
			RegexOptions.CultureInvariant | RegexOptions.Compiled);
		/// <summary>
		/// <para>Produces an approximate plain-text rendition of an HTML event message, examining at most <paramref name="inputLimit"/> characters of it.  See <see cref="ToPlainText(string)"/>.</para>
		/// </summary>
		/// <param name="html">HTML event message.</param>
		/// <param name="inputLimit">Maximum number of characters of the message to examine.  Use int.MaxValue to convert the whole message.</param>
		/// <param name="preserveLineBreaks">If true, line breaks are kept: block-level elements and &lt;br&gt; become line breaks, and so do the line breaks inside the message source (which matter in &lt;pre&gt; blocks such as stack traces).  If false, all whitespace collapses to single spaces, for one-line previews.</param>
		/// <returns>Plain text.  Returns null if [html] is null.</returns>
		public static string ToPlainText(string html, int inputLimit, bool preserveLineBreaks = false)
		{
			if (html == null)
				return null;
			string str = html.Length > inputLimit ? html.Substring(0, inputLimit) : html;
			str = scriptOrStyleBlock.Replace(str, " ");
			if (preserveLineBreaks)
				str = lineBreakingTag.Replace(str, "\n");
			str = htmlTag.Replace(str, " ");
			// Numeric character references above U+FFFF (e.g. "&#128512;") are decoded here, explicitly, into surrogate
			// pairs.  Some HTML decoders mangle them because they don't fit in a single UTF-16 char.
			str = astralNumericCharacterReference.Replace(str, m =>
			{
				long codePoint;
				bool ok = m.Groups["hex"].Success
					? long.TryParse(m.Groups["hex"].Value, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out codePoint)
					: long.TryParse(m.Groups["dec"].Value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out codePoint);
				if (ok && codePoint > 0xFFFF && codePoint <= 0x10FFFF)
					return char.ConvertFromUtf32((int)codePoint);
				return m.Value;
			});
			str = WebUtility.HtmlDecode(str);
			if (preserveLineBreaks)
			{
				str = str.Replace("\r\n", "\n").Replace('\r', '\n');
				str = horizontalWhitespaceRun.Replace(str, " ");
				str = spaceAroundNewline.Replace(str, "\n");
				str = excessNewlines.Replace(str, "\n\n").Trim();
			}
			else
				str = whitespaceRun.Replace(str, " ").Trim();
			if (str.Length == 0)
				return "[HTML message]";
			return str;
		}
		#endregion
	}
}
