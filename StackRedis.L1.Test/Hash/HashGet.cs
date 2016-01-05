﻿using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StackExchange.Redis;
using StackRedis.L1.MemoryCache;
using System.Threading.Tasks;

namespace StackRedis.L1.Test
{
    [TestClass]
    public class HashGet : UnitTestBase
    {
        [TestMethod]
        public void HashGet_Simple()
        {
            _redisDirectDb.HashSet("hashKey", "key1", "value1");
            Assert.AreEqual("value1", (string)_memDb.HashGet("hashKey", "key1"));
            Assert.AreEqual(1, CallsByMemDb);

            //value1 should be mem cached
            Assert.AreEqual("value1", (string)_memDb.HashGet("hashKey", "key1"));
            Assert.AreEqual(1, CallsByMemDb); //no extra call is made to redis
        }

        [TestMethod]
        public async Task HashGet_StringChangedInRedis()
        {
            //Set it and retrieve it into memory
            _redisDirectDb.HashSet("hashKey", "key1", "value1");
            Assert.AreEqual("value1", (string)_memDb.HashGet("hashKey", "key1"));
            
            //Now change it via the other client
            _otherClientDb.HashSet("hashKey", "key1", "value2");

            //Wait for it to propagate and re-retrieve
            await Task.Delay(50);
            Assert.AreEqual("value2", (string)_memDb.HashGet("hashKey", "key1"));
        }

        [TestMethod]
        public void HashGet_Simple_Multi_BothValuesCached()
        {
            _redisDirectDb.HashSet("hashKey", "key1", "value1");
            _redisDirectDb.HashSet("hashKey", "key2", "value2");
            var values = _memDb.HashGet("hashKey", new RedisValue[] { "key1", "key2" });
            Assert.AreEqual("value1", (string)values[0]);
            Assert.AreEqual("value2", (string)values[1]);
            Assert.AreEqual(1, CallsByMemDb);
            
            //Original values should be cached without further calls to redis
            values = _memDb.HashGet("hashKey", new RedisValue[] { "key1", "key2" });
            Assert.AreEqual("value1", (string)values[0]);
            Assert.AreEqual("value2", (string)values[1]);
            Assert.AreEqual(1, CallsByMemDb);
        }

        [TestMethod]
        public async Task HashGet_Multi()
        {
            //Use the memory DB to set keys
            _memDb.HashSet("hashKey", "key1", "value1");
            _memDb.HashSet("hashKey", "key2", "value2");

            //Wait for notifications to arrive from setting both keys
            await Task.Delay(50);

            Assert.AreEqual(2, CallsByMemDb);
            var values = _memDb.HashGet("hashKey", new RedisValue[] { "key1", "key2" });
            Assert.AreEqual("value1", (string)values[0]);
            Assert.AreEqual("value2", (string)values[1]);
            Assert.AreEqual(2, CallsByMemDb);
        }

        [TestMethod]
        public async Task HashGet_Multi_OtherClient()
        {
            //Use the memory DB to set keys
            _memDb.HashSet("hashKey", "key1", "value1");
            _otherClientDb.HashSet("hashKey", "key2", "value2");

            //Wait for notifications to arrive from setting both keys
            await Task.Delay(50);

            Assert.AreEqual(1, CallsByMemDb);
            var values = _memDb.HashGet("hashKey", new RedisValue[] { "key1", "key2" });
            Assert.AreEqual("value1", (string)values[0]);
            Assert.AreEqual("value2", (string)values[1]);
            Assert.AreEqual(2, CallsByMemDb);
        }

        [TestMethod]
        public void HashGet_Simple_Multi_OneValueCached()
        {
            _redisDirectDb.HashSet("hashKey", "key1", "value1");
            _redisDirectDb.HashSet("hashKey", "key2", "value2");
            var values = _memDb.HashGet("hashKey", new RedisValue[] { "key1" });
            Assert.AreEqual("value1", (string)values[0]);

            //only key 2 should need to be retrieved this time. We prove by removing key1 from redis only - not memory
            _redisDirectDb.HashDelete("hashKey", "key1");
            _redisDirectDb.HashSet("hashKey", "key2", "value2");

            //key1 should be cached, key2 should be retrieved
            values = _memDb.HashGet("hashKey", new RedisValue[] { "key1", "key2" });
            Assert.AreEqual("value1", (string)values[0]);
            Assert.AreEqual("value2", (string)values[1]);
        }

        [TestMethod]
        public async Task HashGet_WithExpiry()
        {
            //Set in redis with an expiry
            _redisDirectDb.HashSet("hashKey", "key_exp", "value1");
            _redisDirectDb.KeyExpire("hashKey", TimeSpan.FromMilliseconds(30));

            //Pull into memory
            Assert.AreEqual("value1", (string)_memDb.HashGet("hashKey", "key_exp"));
            Assert.AreEqual(1, CallsByMemDb, "value1 should be pulled into memory");

            //Test that it's set in mem
            Assert.AreEqual("value1", (string)_memDb.HashGet("hashKey", "key_exp"));
            Assert.AreEqual(1, CallsByMemDb, "value1 should be already set in memory");

            await Task.Delay(200);

            //Get it again - should go back to redis, where it's now not set since it's expired
            Assert.IsFalse(_memDb.HashGet("hashKey", "key_exp").HasValue);
            Assert.AreEqual(2, CallsByMemDb);
        }
    }
}
