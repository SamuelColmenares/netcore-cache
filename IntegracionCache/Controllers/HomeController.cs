using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using IntegracionCache.Models;
using Microsoft.Extensions.Caching.Memory;
using System.Net.Http;
using Newtonsoft.Json;
using Microsoft.Extensions.Caching.Distributed;
using System.Text;

namespace IntegracionCache.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IMemoryCache _cache;
        private readonly IDistributedCache _redisCache;
        const string USERKEY = "users";

        public HomeController(ILogger<HomeController> logger, IMemoryCache cacheMemoria, IDistributedCache redis)
        {
            _logger = logger;
            _cache = cacheMemoria;
            _redisCache = redis;
        }

        public async Task<IActionResult> Index()
        {

            bool esCache = true;

            if (!_cache.TryGetValue(USERKEY, out UserResponse result))
            {
                esCache = false;
                using (var httpClient = new HttpClient())
                {
                    string apiResult = await httpClient.GetStringAsync("https://reqres.in/api/users?page=2");
                    result = JsonConvert.DeserializeObject<UserResponse>(apiResult);

                    if (result != null)
                    {
                        var cacheExpirationOptions = new MemoryCacheEntryOptions
                        {
                            AbsoluteExpiration = DateTime.Now.AddHours(1),
                            Priority = CacheItemPriority.Normal,
                            SlidingExpiration = TimeSpan.FromMinutes(5)
                        };

                        _cache.Set(USERKEY, result, cacheExpirationOptions);
                    }
                }
            }

            ViewBag.Users = result;
            ViewBag.esCache = esCache;
            return View();
        }


        public async Task<IActionResult> Privacy()
        {
            bool esCache = true;
            UserResponse result = null;

            var encUsers = await _redisCache.GetAsync(USERKEY);

            if (encUsers != null)
            {
                result = JsonConvert.DeserializeObject<UserResponse>(Encoding.UTF8.GetString(encUsers));
            }
            else
            {
                esCache = false;
                using (var httpClient = new HttpClient())
                {
                    string apiResult = await httpClient.GetStringAsync("https://reqres.in/api/users?page=1");
                    result = JsonConvert.DeserializeObject<UserResponse>(apiResult);

                    if (result != null)
                    {
                        var cacheExpirationOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = DateTime.Now.AddHours(1)
                        };

                        await _redisCache.SetStringAsync(USERKEY, apiResult, cacheExpirationOptions);
                    }
                }
            }

            ViewBag.Users = result;
            ViewBag.esCache = esCache;
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
