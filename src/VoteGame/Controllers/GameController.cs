using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Threading.Tasks;
using VoteGame.Services;

namespace VoteGame.Controllers
{
    [Route("game")]
    public class GameController : Controller
    {
        private IHostingEnvironment _env = null;
        private WebSocketPoolService _pool = null;
        public GameController(IHostingEnvironment env, WebSocketPoolService pool)
        {
            _env = env;
            _pool = pool;
        }

        [HttpGet]
        [Route("socket")]
        public HttpResponseMessage RequestSocket()
        {
            if (HttpContext.WebSockets.IsWebSocketRequest == true)
            {
                if (HttpContext.Request.Headers.ContainsKey("Sec-WebSocket-Key"))
                {
                    HttpContext.WebSockets.AcceptWebSocketAsync().ContinueWith((tsk) =>
                    {
                        WebSocket socket = tsk.Result;
                        WebSocketWrapper wrapper = _pool.AddClient(HttpContext, socket);
                        wrapper.LoopTask.Wait();
                    }).Wait();
                    return new HttpResponseMessage(HttpStatusCode.NoContent);
                }
                else
                    return new HttpResponseMessage(HttpStatusCode.BadRequest);
            }
            else
                return new HttpResponseMessage(HttpStatusCode.SwitchingProtocols);
        }

        [HttpPost]
        [Route("change")]
        public IActionResult Change(string title, string left, string right, int lc = 0, int rc = 0)
        {
            _pool.BoardCurrent.Title = title;
            _pool.BoardCurrent.LeftImage = left;
            _pool.BoardCurrent.RightImage = right;
            _pool.BoardCurrent.LeftCount = lc;
            _pool.BoardCurrent.RightCount = rc;
            return new JsonResult(new { result = true });
        }

        [HttpPost]
        [Route("submit")]
        public IActionResult SubmitEdit(string title, IList<IFormFile> leftpic, IList<IFormFile> rightpic)
        {
            if (title == null || title.Length <= 0 || leftpic.Count <= 0 || rightpic.Count <= 0)
                return new JsonResult(new { result = false });

            IFormFile left = leftpic[0];
            IFormFile right = rightpic[0];

            string leftname = Path.GetFileName(left.FileName.Trim());
            string rightname = Path.GetFileName(right.FileName.Trim());
            string leftpath = Path.Combine(_env.WebRootPath, "images", "left_" + leftname);
            using (FileStream fs = System.IO.File.Open(leftpath, FileMode.OpenOrCreate, FileAccess.Write))
            {
                fs.SetLength(0);
                left.CopyTo(fs);
                fs.Flush();
            }
            string rightpath = Path.Combine(_env.WebRootPath, "images", "right_" + rightname);
            using (FileStream fs = System.IO.File.Open(rightpath, FileMode.OpenOrCreate, FileAccess.Write))
            {
                fs.SetLength(0);
                right.CopyTo(fs);
                fs.Flush();
            }
            _pool.SubmitEdit(title, "images/left_" + leftname, "images/right_" + rightname);
            return new JsonResult(new { result = true });
        }

        [HttpPost]
        [Route("requestedit")]
        public IActionResult RequestEdit()
        {
            if (_pool.CanEdit() == false)
                return new JsonResult(new { result = false });
            else
            {
                _pool.RequestEdit();
                return new JsonResult(new { result = true });
            }
        }
        [HttpPost]
        [Route("canceledit")]
        public IActionResult CancelEdit()
        {
            _pool.CancelEdit();
            return new JsonResult(new { result = true });
        }
    }
}
