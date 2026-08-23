using System;
using System.Net;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;

namespace DialogSystem.EditorTools.PublicInformation
{
    internal sealed class DialogPublicInformationWindow : EditorWindow
    {
        private const int RequestTimeoutSeconds = 8;

        private string _topicId;
        private Label _statusLabel;
        private Label _contentLabel;
        private UnityWebRequest _request;

        public static void Open(string topicId, bool bundledOnly)
        {
            var entry = DialogPublicInformationCatalog.Get(topicId);
            if (entry == null)
            {
                Debug.LogWarning($"[DialogPublicInformation] Unknown topic '{topicId}'.");
                return;
            }

            var window = CreateInstance<DialogPublicInformationWindow>();
            window._topicId = entry.Id;
            window.titleContent = new GUIContent(entry.Title);
            window.minSize = new Vector2(620f, 420f);
            window.BuildUi();
            window.Show();

            if (bundledOnly)
            {
                window.ShowBundled("Bundled copy selected");
            }
            else
            {
                window.LoadOnline();
            }
        }

        private void OnDisable()
        {
            DisposeRequest(abort: true);
        }

        private void BuildUi()
        {
            var entry = DialogPublicInformationCatalog.Get(_topicId);
            rootVisualElement.Clear();
            rootVisualElement.style.paddingLeft = 12f;
            rootVisualElement.style.paddingRight = 12f;
            rootVisualElement.style.paddingTop = 12f;
            rootVisualElement.style.paddingBottom = 12f;

            var title = new Label(entry.Title);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 17f;
            title.style.marginBottom = 8f;
            rootVisualElement.Add(title);

            _statusLabel = new Label("Loading online copy…");
            _statusLabel.style.marginBottom = 8f;
            rootVisualElement.Add(_statusLabel);

            var actions = new VisualElement();
            actions.style.flexDirection = FlexDirection.Row;
            actions.style.marginBottom = 8f;

            actions.Add(new Button(LoadOnline) { text = "Reload Online" });
            actions.Add(new Button(() => DialogPublicInformationCatalog.OpenExternal(_topicId)) { text = "Open in Browser" });
            actions.Add(new Button(() => ShowBundled("Bundled copy selected")) { text = entry.BundledActionLabel });
            rootVisualElement.Add(actions);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1f;
            _contentLabel = new Label();
            _contentLabel.style.whiteSpace = WhiteSpace.Normal;
            _contentLabel.style.unityTextAlign = TextAnchor.UpperLeft;
            scroll.Add(_contentLabel);
            rootVisualElement.Add(scroll);
        }

        private void LoadOnline()
        {
            var entry = DialogPublicInformationCatalog.Get(_topicId);
            if (entry == null)
            {
                return;
            }

            DisposeRequest(abort: true);
            _statusLabel.text = "Loading online copy…";
            _contentLabel.text = string.Empty;

            var request = UnityWebRequest.Get(entry.OnlineUrl);
            request.timeout = RequestTimeoutSeconds;
            _request = request;
            try
            {
                request.SendWebRequest().completed += _ => CompleteOnlineRequest(request);
            }
            catch (Exception exception)
            {
                DisposeRequest(abort: false);
                ShowBundled(exception.Message);
            }
        }

        private void CompleteOnlineRequest(UnityWebRequest request)
        {
            if (request != _request)
            {
                return;
            }

            var succeeded = request.result == UnityWebRequest.Result.Success;
            var response = succeeded ? ToReadableText(request.downloadHandler?.text) : string.Empty;
            var reason = succeeded ? "Empty online response" : DescribeFailure(request);
            var resolved = DialogPublicInformationCatalog.Resolve(_topicId, succeeded, response, reason);
            ShowResolved(resolved);
            DisposeRequest(abort: false);
        }

        private void ShowBundled(string reason)
        {
            DisposeRequest(abort: true);
            ShowResolved(DialogPublicInformationCatalog.Resolve(_topicId, false, string.Empty, reason));
        }

        private void ShowResolved(DialogPublicInformationContent resolved)
        {
            if (_statusLabel == null || _contentLabel == null)
            {
                return;
            }

            _statusLabel.text = resolved.StatusMessage;
            _contentLabel.text = resolved.Content;
        }

        private void DisposeRequest(bool abort)
        {
            var request = _request;
            if (request == null)
            {
                return;
            }

            _request = null;

            if (abort && !request.isDone)
            {
                request.Abort();
            }

            request.Dispose();
        }

        private static string DescribeFailure(UnityWebRequest request)
        {
            if (request == null)
            {
                return "Online request unavailable";
            }

            if (!string.IsNullOrWhiteSpace(request.error))
            {
                return request.error;
            }

            return request.result switch
            {
                UnityWebRequest.Result.ConnectionError => "Connection failed",
                UnityWebRequest.Result.ProtocolError => $"HTTP {request.responseCode}",
                UnityWebRequest.Result.DataProcessingError => "Online response could not be read",
                _ => "Online copy unavailable"
            };
        }

        private static string ToReadableText(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return string.Empty;
            }

            var text = Regex.Replace(source, "<script\\b[^<]*(?:(?!</script>)<[^<]*)*</script>", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            text = Regex.Replace(text, "<style\\b[^<]*(?:(?!</style>)<[^<]*)*</style>", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            text = Regex.Replace(text, "<(br|/p|/div|/li|/h[1-6])[^>]*>", "\n", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, "<[^>]+>", " ");
            text = WebUtility.HtmlDecode(text);
            text = Regex.Replace(text, "[ \\t]+", " ");
            text = Regex.Replace(text, "\\n\\s*\\n\\s*\\n+", "\n\n");
            return text.Trim();
        }
    }
}
