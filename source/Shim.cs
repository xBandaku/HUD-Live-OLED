namespace HudWall
{
    /// <summary>
    /// Injected before the page runs: serves the page's stats/audio fetches from
    /// data pushed by the host process, so no local server is needed.
    /// </summary>
    internal static class Shim
    {
        public const string Js = """
        (function () {
          if (window.__hudNativeShim) return;
          window.__hudNativeShim = true;

          var store = { stats: null, audio: null };

          try {
            if (window.chrome && window.chrome.webview) {
              window.chrome.webview.addEventListener('message', function (e) {
                var m = e.data;
                if (m && m.hud && m.data) {
                  try { store[m.hud] = JSON.stringify(m.data); } catch (err) {}
                }
              });
            }
          } catch (err) {}

          var realFetch = window.fetch ? window.fetch.bind(window) : null;

          window.fetch = function (input, init) {
            var url = '';
            try { url = (typeof input === 'string') ? input : ((input && input.url) || ''); } catch (err) {}
            var key = null;
            if (url.indexOf('/audio') >= 0) key = 'audio';
            else if (url.indexOf('/stats') >= 0) key = 'stats';

            if (key) {
              if (store[key]) {
                return Promise.resolve(new Response(store[key], {
                  status: 200,
                  headers: { 'Content-Type': 'application/json', 'Cache-Control': 'no-store' }
                }));
              }
              return new Promise(function (resolve) {
                setTimeout(function () {
                  resolve(new Response(store[key] || '{}', {
                    status: 200,
                    headers: { 'Content-Type': 'application/json' }
                  }));
                }, 150);
              });
            }
            if (realFetch) return realFetch(input, init);
            return Promise.reject(new Error('fetch unavailable'));
          };
        })();
        """;
    }
}
