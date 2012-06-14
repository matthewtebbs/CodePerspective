/*
 * 	MuddyTummy Core
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

/* System */
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Security.Permissions;
using System.Threading;

namespace MuddyTummy.Collections
{
	[Serializable]
	public class LockableValue<TV>
	{
		/*
		 * Member variables.
		 */
		private readonly TV _val;
		[NonSerialized] private long _lockcount;
		
		/*
		 * Construction/destruction.
		 */
		public LockableValue(TV val)
		{
			_val = val;
			_lockcount = 0;
		}
		
		/*
		 * Properties.
		 */
		public TV Value {get {return _val;}}
		public bool IsLocked {get {return 0 < Interlocked.CompareExchange(ref _lockcount, 0, 0);}}
			
		/*
		 * Methods.
		 */
		public void Lock() {Interlocked.Increment(ref _lockcount);}
		public void Unlock() {Interlocked.Decrement(ref _lockcount);}
	}
	
	public class LockableValueRef<TV> : IDisposable
	{
		/*
		 * Member variables.
		 */
		private LockableValue<TV> _lockableval;
		
		/*
		 * Construction/destruction.
		 */
		internal LockableValueRef(LockableValue<TV> lockableval)
		{
			if (null == lockableval)
				throw new ArgumentNullException();
			
			_lockableval = lockableval;
			_lockableval.Lock();
		}
		
		~LockableValueRef()
		{
			Dispose(false);
		}
		
		public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }	
		
		protected void Dispose(bool disposing)
		{
			if (disposing)
			{
				if (null != _lockableval)
				{
					_lockableval.Unlock();
					_lockableval = null;
				}
			}
		}
		
		/*
		 * Properties.
		 */
		public TV Value {get {return _lockableval.Value;}}
	}
	
	public interface ILRUCacheEntryHandler<TK, TV>
	{
		long SizeOfValue(KeyValuePair<TK, TV> entry);
		void DisposeOfValue(KeyValuePair<TK, TV> entry);
	}
		
	[Serializable]
	public sealed class LRUCache<TK, TV> : IEnumerable<KeyValuePair<TK, LockableValue<TV>>>, ILinkedDictionaryCtl<TK, LockableValue<TV>>, ISerializable
	{
		/*
		 * Constants.
		 */
		private const string cstrMaxSize = "MaxSize";
		private const string cstrLinkedDict = "LinkedDict";
		private const string cstrCacheEntryHdlr = "CacheEntryHdlr";
		private const string cstrAutoPurge = "AutoPurge";
		private const string cstrCurrentSize = "CurrentSize";
		
		/*
		 * Member variables.
		 */
		private readonly long _maxSize;
		private readonly LinkedDictionary<TK, LockableValue<TV>> _linkeddict;
		private readonly ILRUCacheEntryHandler<TK, TV> _lrucacheentryhdlr;
		private bool _autoPurge = true;
		private long _currentSize = 0;
		
		/*
		 * Construction/destruction.
		 */
		public LRUCache(long maxSize, ILRUCacheEntryHandler<TK, TV> lrucacheentryhdlr, IEqualityComparer<TK> comparer)
		{
			if (0 == maxSize)
				throw new ArgumentOutOfRangeException();
			
			if (null == lrucacheentryhdlr)
				throw new ArgumentNullException();
			
			_maxSize = maxSize;
			_linkeddict = new LinkedDictionary<TK, LockableValue<TV>>(16 /* initial capacity */, comparer, true /* accessor order */, this);
			_lrucacheentryhdlr = lrucacheentryhdlr;
		}	
		public LRUCache(long maxSize, ILRUCacheEntryHandler<TK, TV> lrucacheentryhdlr) : this(maxSize, lrucacheentryhdlr, null) {}
		
 		private LRUCache(SerializationInfo srlzinfo, StreamingContext context)
		{
			if (null == srlzinfo)
				throw new ArgumentNullException();
			
			_maxSize = srlzinfo.GetInt32(cstrMaxSize);
			_linkeddict = srlzinfo.GetValue(cstrLinkedDict, typeof(LinkedDictionary<TK, LockableValue<TV>>)) as LinkedDictionary<TK, LockableValue<TV>>;
			_lrucacheentryhdlr = srlzinfo.GetValue(cstrCacheEntryHdlr, typeof(ILRUCacheEntryHandler<TK, TV>)) as ILRUCacheEntryHandler<TK, TV>;
			_autoPurge = srlzinfo.GetBoolean(cstrAutoPurge);
			_currentSize = srlzinfo.GetInt32(cstrCurrentSize);
       }
		
		/*
		 * Implementation of ISerializable.
		 */
		[SecurityPermission(SecurityAction.LinkDemand, Flags = SecurityPermissionFlag.SerializationFormatter)]
		public void GetObjectData(SerializationInfo srlzinfo, StreamingContext context)
		{
			if (null == srlzinfo)
				throw new ArgumentNullException();
			
			srlzinfo.AddValue(cstrMaxSize, _maxSize);
			srlzinfo.AddValue(cstrLinkedDict, _linkeddict);
			srlzinfo.AddValue(cstrCacheEntryHdlr, _lrucacheentryhdlr);
			srlzinfo.AddValue(cstrAutoPurge, _autoPurge);
			srlzinfo.AddValue(cstrCurrentSize, _currentSize);
		}
		
		/*
		 * Properties.
		 */
		public long MaxSize {get {return _maxSize;}}			
		public long CurrentSize {get {return _currentSize;}}
		
		public int Count {get {return _linkeddict.Count;}}	
		
		public bool AutoPurge
		{
			get {return _autoPurge;}
			set {_autoPurge = value;}
		}
		
		/*
		 * Methods.
		 */
		public LockableValueRef<TV> Get(TK key)
		{
			LockableValue<TV> lockablevalue;
			if (!_linkeddict.TryGetValue(key, out lockablevalue))
				return null;
			return new LockableValueRef<TV>(lockablevalue);
		}
		
		public void Put(TK key, TV val)
		{
			_linkeddict.Put(key, new LockableValue<TV>(val));
		}
		
		private bool PurgeKeyPredicate(KeyValuePair<TK, LockableValue<TV>> pair, out bool doStop)
		{
			doStop = true;
			return !pair.Value.IsLocked;
		}
		
		private bool PurgePredicate(KeyValuePair<TK, LockableValue<TV>> pair, out bool doStop)
		{
			doStop = _currentSize <= _maxSize;
			return !(doStop || pair.Value.IsLocked);
		}
		
		private bool ClearPredicate(KeyValuePair<TK, LockableValue<TV>> pair, out bool doStop)
		{
			doStop = false;
			return !pair.Value.IsLocked;
		}
		
		public void Purge(TK key)
		{
			_linkeddict.Remove(key, PurgeKeyPredicate);
		}
		
		public void Purge()
		{
			_linkeddict.Clear(PurgePredicate);
		}

		public void Clear()
		{
			_linkeddict.Clear(ClearPredicate);
		}
		
		/*
		 * Implementation of ILinkedDictionaryCtl<TK, LockableValue<TV>>.
		 */
		public void DidAddPair(KeyValuePair<TK, LockableValue<TV>> pair)
		{
			KeyValuePair<TK, TV> entry = new KeyValuePair<TK, TV>(pair.Key, pair.Value.Value);
			
			long sizeEntry = _lrucacheentryhdlr.SizeOfValue(entry);			
			_currentSize += sizeEntry;
			
			if (_autoPurge) Purge();
		}
		
		public void DidRemovePair(KeyValuePair<TK, LockableValue<TV>> pair)
		{
			if (pair.Value.IsLocked)
				throw new InvalidOperationException();
			
			KeyValuePair<TK, TV> entry = new KeyValuePair<TK, TV>(pair.Key, pair.Value.Value);
			
			long sizeEntry = _lrucacheentryhdlr.SizeOfValue(entry);			
			_currentSize -= sizeEntry;
			
			_lrucacheentryhdlr.DisposeOfValue(entry);
		}
		
		/*
		 * Implementation of IEnumerable<KeyValuePair<TK, TV>>
		 */
		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {return GetEnumerator();}
		public IEnumerator<KeyValuePair<TK, LockableValue<TV>>> GetEnumerator() {return _linkeddict.GetEnumerator();}
	}
}