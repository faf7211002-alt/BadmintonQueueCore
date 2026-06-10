using Microsoft.AspNetCore.Mvc;
using BadmintonQueueCore.Data;
using BadmintonQueueCore.Models;

namespace BadmintonQueueCore.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _db;

        public HomeController(AppDbContext db)
        {
            _db = db;
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

            ViewBag.CourtAPlayers = _db.Players
                .Where(p => p.Status == "Playing" && p.CourtNo == 1)
                .OrderBy(p => p.QueueNo)
                .ToList();

            ViewBag.CourtBPlayers = _db.Players
                .Where(p => p.Status == "Playing" && p.CourtNo == 2)
                .OrderBy(p => p.QueueNo)
                .ToList();

            return View();
        }

        [HttpPost]
        public IActionResult AddPlayer(string playerName)
        {
            if (!string.IsNullOrWhiteSpace(playerName))
            {
                int nextQueueNo = GetNextQueueNo();

                _db.Players.Add(new Player
                {
                    PlayerName = playerName.Trim(),
                    QueueNo = nextQueueNo,
                    Status = "Waiting",
                    CourtNo = 0,
                    GroupNo = 0
                });

                _db.SaveChanges();
            }

            return RedirectToAction("Index");
        }
        [HttpPost]
        public IActionResult MoveToReady(int id, int groupNo)
        {
            if (groupNo < 1 || groupNo > 5)
            {
                return RedirectToAction("Index");
            }

            int readyCount = _db.Players.Count(p => p.Status == "Ready" && p.GroupNo == groupNo);

            if (readyCount >= 4)
            {
                TempData["Message"] = $"備戰區 {groupNo} 已滿，最多只能 4 人";
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
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult MoveToCourt(int id, int courtNo)
        {
            if (courtNo != 1 && courtNo != 2)
            {
                return RedirectToAction("Index");
            }

            int courtCount = _db.Players.Count(p => p.Status == "Playing" && p.CourtNo == courtNo);

            if (courtCount >= 4)
            {
                TempData["Message"] = courtNo == 1
       ? "A場已滿，最多只能 4 人"
       : "B場已滿，最多只能 4 人";

                return RedirectToAction("Index");
            }

            var player = _db.Players.Find(id);

            if (player != null)
            {
                player.Status = "Playing";
                player.CourtNo = courtNo;
                player.GroupNo = 0;

                _db.SaveChanges();
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult MoveWaitingToCourt(int id, int courtNo)
        {
            if (courtNo != 1 && courtNo != 2)
            {
                return RedirectToAction("Index");
            }

            int courtCount = _db.Players.Count(p => p.Status == "Playing" && p.CourtNo == courtNo);

            if (courtCount >= 4)
            {
                TempData["Message"] = courtNo == 1
                    ? "A場已滿，最多只能 4 人"
                    : "B場已滿，最多只能 4 人";

                return RedirectToAction("Index");
            }

            var player = _db.Players.Find(id);

            if (player != null)
            {
                player.Status = "Playing";
                player.CourtNo = courtNo;
                player.GroupNo = 0;

                _db.SaveChanges();
            }

            return RedirectToAction("Index");
        }


        [HttpPost]
        public IActionResult MoveReadyGroupToCourt(int groupNo, int courtNo)
        {
            if (groupNo < 1 || groupNo > 5 || (courtNo != 1 && courtNo != 2))
            {
                return RedirectToAction("Index");
            }

            var groupPlayers = _db.Players
                .Where(p => p.Status == "Ready" && p.GroupNo == groupNo)
                .OrderBy(p => p.QueueNo)
                .ToList();

            if (!groupPlayers.Any())
            {
                return RedirectToAction("Index");
            }

            int courtCount = _db.Players.Count(p => p.Status == "Playing" && p.CourtNo == courtNo);
            int totalAfterMove = courtCount + groupPlayers.Count;

            if (totalAfterMove > 4)
            {
                TempData["Message"] = courtNo == 1
                    ? $"A場目前已有 {courtCount} 人，備戰區 {groupNo} 有 {groupPlayers.Count} 人，上場後會超過 4 人"
                    : $"B場目前已有 {courtCount} 人，備戰區 {groupNo} 有 {groupPlayers.Count} 人，上場後會超過 4 人";

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

            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult BackToWaiting(int id)
        {
            var player = _db.Players.Find(id);

            if (player != null)
            {
                player.Status = "Waiting";
                player.CourtNo = 0;
                player.GroupNo = 0;
                player.QueueNo = GetNextQueueNo();

                _db.SaveChanges();
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult DeletePlayer(int id)
        {
            var player = _db.Players.Find(id);

            if (player != null)
            {
                _db.Players.Remove(player);
                _db.SaveChanges();
            }

            return RedirectToAction("Index");
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
    }
}
