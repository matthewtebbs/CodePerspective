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

namespace MuddyTummy.Collections
{
	public interface ILinkedDictionaryCtl<TK, TV>
	{
		void DidAddPair(KeyValuePair<TK, TV> pair);
		void DidRemovePair(KeyValuePair<TK, TV> pair);
	}
	
	[Serializable]
	public class LinkedDictionary<TK, TV> : IDictionary<TK, TV>, IDeserializationCallback, ISerializable
	{
		/*
		 * Constants.
		 */
		private const string cstrInAccessOrder = "InAccessOrder";
		private const string cstrOrderedList = "OrderedList";
		private const string cstrLinkedDictCtl = "LinkedDictCtl";
		private const string cstrDictComparer = "DictComparer";
		
		/*
		 * Member variables.
		 */
		private readonly bool _inAccessOrder;
		private readonly LinkedList<KeyValuePair<TK, TV>> _orderedlist;
		private readonly Dictionary<TK, LinkedListNode<KeyValuePair<TK, TV>>> _dict;
		private readonly ILinkedDictionaryCtl<TK, TV> _linkeddictctl;
		
		/*
		 * Construction/destruction.
		 */
		public LinkedDictionary(int initCapacity, IEqualityComparer<TK> comparer, bool inAccessOrder, ILinkedDictionaryCtl<TK, TV> linkeddictctl)
		{
			_inAccessOrder = inAccessOrder;
			_orderedlist = new LinkedList<KeyValuePair<TK, TV>>();
			_dict = new Dictionary<TK, LinkedListNode<KeyValuePair<TK, TV>>>(initCapacity, comparer);
			_linkeddictctl = linkeddictctl;
		}
		public LinkedDictionary(int initCapacity, IEqualityComparer<TK> comparer) : this(initCapacity, comparer, false, null) {}
		public LinkedDictionary(int initCapacity, bool inAccessOrder) : this(initCapacity, null, inAccessOrder, null) {}
		public LinkedDictionary(int initCapacity) : this(initCapacity, null, false, null) {}
		public LinkedDictionary() : this(16, null, false, null) {}
		
        protected LinkedDictionary(SerializationInfo srlzinfo, StreamingContext context)
        {
			if (null == srlzinfo)
				throw new ArgumentNullException();
			
			IEqualityComparer<TK> comparer = srlzinfo.GetValue(cstrDictComparer, typeof(IEqualityComparer<TK>)) as IEqualityComparer<TK>;
			
			_inAccessOrder = srlzinfo.GetBoolean(cstrInAccessOrder);
			_orderedlist = srlzinfo.GetValue(cstrOrderedList, typeof(LinkedList<KeyValuePair<TK, TV>>)) as LinkedList<KeyValuePair<TK, TV>>;
			_dict = new Dictionary<TK, LinkedListNode<KeyValuePair<TK, TV>>>(_orderedlist.Count, comparer);
			_linkeddictctl = srlzinfo.GetValue(cstrLinkedDictCtl, typeof(ILinkedDictionaryCtl<TK, TV>)) as ILinkedDictionaryCtl<TK, TV>;
		}
		
		/*
		 * Implementation of IDeserializationCallback.
		 */
		public virtual void OnDeserialization(object sender)
		{
 			for (LinkedListNode<KeyValuePair<TK, TV>> node = _orderedlist.First; null != node; node = node.Next)
				_dict[node.Value.Key] = node;
		}
		
		/*
		 * Implementation of ISerializable.
		 */
		[SecurityPermission(SecurityAction.LinkDemand, Flags = SecurityPermissionFlag.SerializationFormatter)]
		public virtual void GetObjectData(SerializationInfo srlzinfo, StreamingContext context)
		{
			if (null == srlzinfo)
				throw new ArgumentNullException();
			
			srlzinfo.AddValue(cstrDictComparer, _dict.Comparer);
			srlzinfo.AddValue(cstrInAccessOrder, _inAccessOrder);
			srlzinfo.AddValue(cstrOrderedList, _orderedlist);
			srlzinfo.AddValue(cstrLinkedDictCtl, _linkeddictctl);
		}
		
		/*
		 * Methods.
		 */
		private void AccessNode(LinkedListNode<KeyValuePair<TK, TV>> node)
		{
			if (!_inAccessOrder) return;
			
			_orderedlist.Remove(node);
			_orderedlist.AddFirst(node);
		}
		
		private void AddNode(LinkedListNode<KeyValuePair<TK, TV>> node)
		{
			_orderedlist.AddFirst(node);
			_dict[node.Value.Key] = node;
		
			if (null != _linkeddictctl)
				_linkeddictctl.DidAddPair(node.Value);
		}
		
		private void RemoveNode(LinkedListNode<KeyValuePair<TK, TV>> node)
		{
			_orderedlist.Remove(node);
			_dict.Remove(node.Value.Key);
			
			if (null != _linkeddictctl)
				_linkeddictctl.DidRemovePair(node.Value);
		}		
		
		/*
		 * Implementation of IDictionary<TK, TV>.
		 */
		public TV this[TK key]
		{
			get
			{
				TV val;
				if (TryGetValue(key, out val))
					return val;
				throw new KeyNotFoundException();
			}
			set
			{
				Put(key, value);
			}
		}
		
		public ICollection<TK> Keys
		{
			get {throw new NotSupportedException();}
		}
		
		public ICollection<TV> Values
		{
			get {throw new NotSupportedException();}
		}
		
		public bool ContainsKey(TK key)
		{
			return _dict.ContainsKey(key);
		}
		
		public bool TryGetValue(TK key, out TV val)
		{
			LinkedListNode<KeyValuePair<TK, TV>> node;
			if (_dict.TryGetValue(key, out node))
			{
				AccessNode(node);			
				val = node.Value.Value;
			}
			else
			{
				val = default(TV);
			}
			return null != node;
		}
		
		public void Add(TK key, TV val)
		{
			if (ContainsKey(key))
			    throw new ArgumentException();
			
			Put(key, val);
		}
		
		public TV Put(TK key, TV val)
		{
			TV valPrior = default(TV);
			
			LinkedListNode<KeyValuePair<TK, TV>> node;		
			if (_dict.TryGetValue(key, out node))
			{
				valPrior = node.Value.Value;
				
				if ((null == valPrior && null == val) || valPrior.Equals(val))
				{
					AccessNode(node);				
					return valPrior;
				}
				
				RemoveNode(node);
			}
			
			AddNode(new LinkedListNode<KeyValuePair<TK, TV>>(new KeyValuePair<TK, TV>(key, val)));
			
			return valPrior;
		}
		
		public bool Remove(TK key)
		{
			return Remove(key, null);
		}
		
		public delegate bool CanRemovePredicate(KeyValuePair<TK, TV> pair, out bool doStop);	
		public bool Remove(TK key, CanRemovePredicate predicate)
		{
			LinkedListNode<KeyValuePair<TK, TV>> node;
			if (_dict.TryGetValue(key, out node))
			{
				bool doStop;
				if (null == predicate || predicate(node.Value, out doStop))
				{
					RemoveNode(node);
					return true;
				}
			}
			
			return false;
		}
		
		public void Clear()
		{
			Clear(null);
		}
		
		public void Clear(CanRemovePredicate predicate)
		{
			LinkedListNode<KeyValuePair<TK, TV>> node = _orderedlist.Last;
			while (null != node)
			{
				LinkedListNode<KeyValuePair<TK, TV>> nodePrev = node.Previous;
				
				bool doStop = false;
				if (null == predicate || predicate(node.Value, out doStop))
					RemoveNode(node);
				
				if (doStop)
					break;
				
				node = nodePrev;
			}
		}
		
		/*
		 * Implementation of ICollection<KeyValuePair<TK, TV>>
		 */
		public int Count
		{
			get {return _orderedlist.Count;}
		}
		
		public bool IsReadOnly
		{
			get {return false;}
		}
		
		/* 'public void Clear()' is above */
		
		public bool Contains(KeyValuePair<TK, TV> pair)
		{
			LinkedListNode<KeyValuePair<TK, TV>> node;
			if (!_dict.TryGetValue(pair.Key, out node))
				return false;
			
			return EqualityComparer<TV>.Default.Equals(node.Value.Value, pair.Value);
		}
		
		public void CopyTo(KeyValuePair<TK, TV>[] array, int arrayIndex)
		{
			throw new NotSupportedException();
		}
		
		public void Add(KeyValuePair<TK, TV> pair)
		{
			Add(pair.Key, pair.Value);
		}
		
		public bool Remove(KeyValuePair<TK, TV> pair)
		{
			return Remove(pair.Key);
		}
		
		/*
		 * Implementation of IEnumerable<KeyValuePair<TK, TV>>
		 */
		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {return GetEnumerator();}
		public IEnumerator<KeyValuePair<TK, TV>> GetEnumerator() {return _orderedlist.GetEnumerator();}
	}
}