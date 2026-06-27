using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using BadmintonQueueCore.Data;
using BadmintonQueueCore.Models;
using BadmintonQueueCore.Hubs;

namespace BadmintonQueueCore.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IHubContext<QueueHub> _hub;
        private const int MaxPlayers = 4;

        public HomeController(AppDbContext db, IHubContext<QueueHub> hub)
        {
            _db = db;
            _hub = hub;
        }

        public IActionResult Index()
        {
            ViewBag.WaitingPlayers = _db.Players
                .Where(p => p.Status == "Waiting")
                .OrderBy(p => p.QueueNo)
                .ToList();

            ViewBag.ReadyGroups = Enumerable.Range(1, 5)
                .ToDictionary(
                    groupNo => groupNo,
                    groupNo => _db.Players
                        .Where(p => p.Status == "Ready" && p.GroupNo == groupNo)
                        .OrderBy(p => p.QueueNo)
                        .ToList()
                );

            ViewBag.CourtAPlayers = GetCourtPlayers(1);
            ViewBag.CourtBPlayers = GetCourtPlayers(2);
            ViewBag.CourtCPlayers = GetCourtPlayers(3);

            ViewBag.TotalCount = _db.Players.Count();
            ViewBag.WaitingCount = _db.Players.Count(p => p.Status == "Waiting");
            ViewBag.ReadyCount = _db.Players.Count(p => p.Status == "Ready");

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> AddPlayer(string playerName)
        {
            if (!string.IsNullOrWhiteSpace(playerName))
            {
                _db.Players.Add(new Player
                {
                    PlayerName = playerName.Trim(),
                    QueueNo = GetNextQueueNo(),
                    Status = "Waiting",
                    CourtNo = 0,
                    GroupNo = 0
                });

                _db.SaveChanges();
                await NotifyRefresh();
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> AddPlayers(string playerNames)
        {
            if (!string.IsNullOrWhiteSpace(playerNames))
            {
                var names = playerNames
                    .Split(new[] { "\r\n", "\n", "\r", "、", ",", "，", " " }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToList();

                foreach (var name in names)
                {
                    _db.Players.Add(new Player
                    {
                        PlayerName = name,
                        QueueNo = GetNextQueueNo(),
                        Status = "Waiting",
                        CourtNo = 0,
                        GroupNo = 0
                    });

                    _db.SaveChanges();
                }

                await NotifyRefresh();
            }

            return RedirectToAction("Index");
        }
        [HttpPost]


    

        public async Task<IActionResult> MoveToReady(int id, int groupNo)
        {
            if (groupNo < 1 || groupNo > 5)
                return RedirectToAction("Index");

            int readyCount = _db.Players.Count(p => p.Status == "Ready" && p.GroupNo == groupNo);

            if (readyCount >= MaxPlayers)
            {
                TempData["Message"] = $"備戰區 {groupNo} 已滿，最多只能 {MaxPlayers} 人";
                return RedirectToAction("Index");
            }

            var player = _db.Players.Find(id);

            if (player != null)
            {
                player.Status = "Ready";
                player.CourtNo = 0;
                player.GroupNo = groupNo;
                player.QueueNo = GetNextQueueNo();
                _db.SaveChanges();
                await NotifyRefresh();
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> MoveToCourt(int id, int courtNo)
        {
            if (courtNo < 1 || courtNo > 3)
                return RedirectToAction("Index");

            int courtCount = GetCourtCount(courtNo);

            if (courtCount >= MaxPlayers)
            {
                TempData["Message"] = $"{GetCourtName(courtNo)}已滿，最多只能 {MaxPlayers} 人";
                return RedirectToAction("Index");
            }

            var player = _db.Players.Find(id);

            if (player != null)
            {
                player.Status = "Playing";
                player.CourtNo = courtNo;
                player.GroupNo = 0;
                _db.SaveChanges();
                await NotifyRefresh();
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> MoveReadyGroupToCourt(int groupNo, int courtNo)
        {
            if (groupNo < 1 || groupNo > 5 || courtNo < 1 || courtNo > 3)
                return RedirectToAction("Index");

            var groupPlayers = _db.Players
                .Where(p => p.Status == "Ready" && p.GroupNo == groupNo)
                .OrderBy(p => p.QueueNo)
                .ToList();

            if (!groupPlayers.Any())
            {
                TempData["Message"] = $"備戰區 {groupNo} 沒有人";
                return RedirectToAction("Index");
            }

            int courtCount = GetCourtCount(courtNo);

            if (courtCount + groupPlayers.Count > MaxPlayers)
            {
                TempData["Message"] =
                    $"{GetCourtName(courtNo)}目前已有 {courtCount} 人，備戰區 {groupNo} 有 {groupPlayers.Count} 人，上場後會超過 {MaxPlayers} 人";
                return RedirectToAction("Index");
            }

            foreach (var player in groupPlayers)
            {
                player.Status = "Playing";
                player.CourtNo = courtNo;
                player.GroupNo = 0;
            }

            ShiftReadyGroups(groupNo);

            _db.SaveChanges();
            await NotifyRefresh();

            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> FinishCourt(int courtNo)
        {
            if (courtNo < 1 || courtNo > 3)
                return RedirectToAction("Index");

            var courtPlayers = _db.Players
                .Where(p => p.Status == "Playing" && p.CourtNo == courtNo)
                .ToList();

            foreach (var player in courtPlayers)
            {
                player.Status = "Waiting";
                player.CourtNo = 0;
                player.GroupNo = 0;
                player.QueueNo = GetNextQueueNo();
            }

            var readyOnePlayers = _db.Players
                .Where(p => p.Status == "Ready" && p.GroupNo == 1)
                .OrderBy(p => p.QueueNo)
                .ToList();

            if (readyOnePlayers.Any() && readyOnePlayers.Count <= MaxPlayers)
            {
                foreach (var player in readyOnePlayers)
                {
                    player.Status = "Playing";
                    player.CourtNo = courtNo;
                    player.GroupNo = 0;
                }

                ShiftReadyGroups(1);
            }

            _db.SaveChanges();
            await NotifyRefresh();

            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> BackToWaiting(int id)
        {
            var player = _db.Players.Find(id);

            if (player != null)
            {
                player.Status = "Waiting";
                player.CourtNo = 0;
                player.GroupNo = 0;
                player.QueueNo = GetNextQueueNo();
                _db.SaveChanges();
                await NotifyRefresh();
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> DeletePlayer(int id)
        {
            var player = _db.Players.Find(id);

            if (player != null)
            {
                _db.Players.Remove(player);
                _db.SaveChanges();
                await NotifyRefresh();
            }

            return RedirectToAction("Index");
        }

        [HttpGet]
        public IActionResult Ping()
        {
            return Ok("alive");
        }

        private async Task NotifyRefresh()
        {
            await _hub.Clients.All.SendAsync("RefreshPage");
        }

        private List<Player> GetCourtPlayers(int courtNo)
        {
            return _db.Players
                .Where(p => p.Status == "Playing" && p.CourtNo == courtNo)
                .OrderBy(p => p.QueueNo)
                .ToList();
        }

        private int GetCourtCount(int courtNo)
        {
            return _db.Players.Count(p => p.Status == "Playing" && p.CourtNo == courtNo);
        }

        private int GetNextQueueNo()
        {
            return _db.Players.Any()
                ? _db.Players.Max(p => p.QueueNo) + 1
                : 1;
        }

        private void ShiftReadyGroups(int startGroupNo)
        {
            for (int i = startGroupNo + 1; i <= 5; i++)
            {
                var players = _db.Players
                    .Where(p => p.Status == "Ready" && p.GroupNo == i)
                    .ToList();

                foreach (var player in players)
                {
                    player.GroupNo = i - 1;
                }
            }
        }

        private string GetCourtName(int courtNo)
        {
            return courtNo switch
            {
                1 => "A場",
                2 => "B場",
                3 => "C場",
                _ => "場地"
            };
        }
    }
}