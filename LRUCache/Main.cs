/*
 * 	LRU Cache Sample
 *
 * Copyright (c) 2010-2012 MuddyTummy Software, LLC
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 */

/*
 * System.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.Serialization.Formatters.Binary;

/*
 * MuddyTummy.
 */
using MuddyTummy.Collections;

namespace LRUCache
{
	class MainClass
	{
		[Serializable]
		struct WebResource
		{
			string _strCachePath;
			long _sizeOnDisk;
			public WebResource(string strCachePath, int sizeOnDisk)
			{
				_strCachePath = strCachePath;
				_sizeOnDisk = sizeOnDisk;
			}
			public string CachePath {get {return _strCachePath;}}
			public long SizeOnDisk
			{
				get
				{
					/*
					 * Could be determined using System.IO.FileInfo.Length for example.
					 */
					return _sizeOnDisk;
				}
			}
		}
		
		[Serializable]
		class CacheEntryHander : ILRUCacheEntryHandler<Uri, WebResource>
		{
			public long SizeOfValue(KeyValuePair<Uri, WebResource> entry)
			{
				/*
				 * Return the calculated size of the value of the entry.
				 * The entry is a key value pair of cache key to value.
				 * 
				 * The calculated size is used by the cache to manage the cache size as per 
				 * the cache's maximum total size (LRUCache.MaxSize) of all entries.
				 * 
				 * In this example the size of the value is the size of the cached
				 * web resource on disk.
				 */
				return entry.Value.SizeOnDisk;
			}
			
			public void DisposeOfValue(KeyValuePair<Uri, WebResource> entry)
			{
				Console.WriteLine(string.Format("CacheEntryHander::DisposeOfValue()"));
				Console.WriteLine(string.Format("<\nUri : {0}\nCachePath : {1}\nSize : {2}\n>",
				                                entry.Key, entry.Value.CachePath, entry.Value.SizeOnDisk));
				
				/*
				 * REQ'D IMPL :
				 * 
				 * Dispose of cache value resources, in this example
				 * we might delete the web cache file on disk using System.IO.File.Delete().
				 */
			}
		}
		
		private static Uri _uriDummyRoot = new Uri("http://codeperspective.net/dummypath");
		private static long _cbMaxCacheSize = 10 * 1024 * 1024; /* 10MB maximum cache size */
		private static long _cbMaxFileSize = 2 * 1024 * 1024; /* 2MB pseudo file sizes for the sake of this example */
		
		static Random random = new Random((int)DateTime.Now.Ticks);
		
		public static void Main(string[] args)
		{
			LRUCache<Uri, WebResource> webcache = new LRUCache<Uri, WebResource>(_cbMaxCacheSize, new CacheEntryHander());

			/*
			 * CODE BLOCK 1 :
			 * 
			 * Exercise cache through the addition of 'count' web resources.
			 */
			for (int count = 0; count < 20; count++)
			{
				Uri uri = new Uri(_uriDummyRoot, string.Format("webresource{0}", count));
				
				using (LockableValueRef<WebResource> refWebResource = webcache.Get(uri))
				{
					if (null != refWebResource)
					{
						/*
						 * Already cached. Note that in this example this will never happen.
						 */
					}
					else
					{
						/*
						 * REQ'D IMPL :
						 * 
						 * Note that in this sample code we perform no actual download of the web resource
			 			 * if it doesn't yet exist in the cache.
			 			 */
						webcache.Put(uri, new WebResource(string.Format("dummy_cache_file{0}", count), random.Next((int)_cbMaxFileSize)));
					}
				}
				
				Console.WriteLine("** Cache has {0} entries with size {1} (max. size {2}) **", webcache.Count, webcache.CurrentSize, webcache.MaxSize);
			}
				
			/*
			 * CODE BLOCK 2 :
			 * 
			 * Serialize out the web cache.
			 * Deserialize back the web cache (in this case to demonstrate a deep clone).
			 */
			BinaryFormatter formatter = new BinaryFormatter();
			using (MemoryStream stream = new MemoryStream(8192))
			{			
#pragma warning disable 0219
				LRUCache<Uri, WebResource> webcacheCloned = null;
#pragma warning restore 0219
				try
				{
					formatter.Serialize(stream, webcache);
					stream.Flush();
					stream.Seek(0, SeekOrigin.Begin);
					webcacheCloned = formatter.Deserialize(stream) as LRUCache<Uri, WebResource>;
				}
				catch (Exception)
				{
					/*
					 * REQ'D IMPL : Handle serialization exception.
					 */
				}
			}
		}
	}
}
