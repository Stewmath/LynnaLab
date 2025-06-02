using System.Collections;

namespace Util
{
    /// <summary>
    /// Like LinkedList<T>, but lookups (for adds/removals/finds) can be done in O(1)-ish time like
    /// a Dictionary. Can't have the same value twice as a consequence.
    ///
    /// By implementing ICollection<T>, this can be serialized and deserialized by System.Text.Json.
    /// </summary>
    public class DictionaryLinkedList<K, V> : IEnumerable<V>
    {
        protected LinkedList<V> linkedList = new LinkedList<V>();
        protected Dictionary<K, LinkedListNode<V>> dict = new Dictionary<K, LinkedListNode<V>>();


        public int Count { get { return linkedList.Count; } }

        public LinkedListNode<V> FirstNode { get { return linkedList.First; } }
        public LinkedListNode<V> LastNode { get { return linkedList.Last; } }

        public V First { get { return FirstNode == null ? default(V) : FirstNode.Value; } }
        public V Last { get { return LastNode == null ? default(V) : LastNode.Value; } }

        public DictionaryLinkedList()
        {
        }

        public IEnumerator<V> GetEnumerator()
        {
            return linkedList.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return linkedList.GetEnumerator();
        }

        public void AddAfter(LinkedListNode<V> node, K key, LinkedListNode<V> newNode)
        {
            if (dict.ContainsKey(key))
                throw new Exception("DictionaryLinkedList already contains value.");
            linkedList.AddAfter(node, newNode);
            dict[key] = newNode;
        }
        public void AddAfter(LinkedListNode<V> node, K key, V newNode)
        {
            AddAfter(node, key, new LinkedListNode<V>(newNode));
        }

        public void AddBefore(LinkedListNode<V> node, K key, LinkedListNode<V> newNode)
        {
            if (dict.ContainsKey(key))
                throw new Exception("DictionaryLinkedList already contains value.");
            linkedList.AddBefore(node, newNode);
            dict[key] = newNode;
        }
        public void AddBefore(LinkedListNode<V> node, K key, V newNode)
        {
            AddBefore(node, key, new LinkedListNode<V>(newNode));
        }

        public void AddFirst(K key, LinkedListNode<V> node)
        {
            if (dict.ContainsKey(key))
                throw new Exception("DictionaryLinkedList already contains value.");
            linkedList.AddFirst(node);
            dict[key] = node;
        }
        public void AddFirst(K key, V value)
        {
            AddFirst(key, new LinkedListNode<V>(value));
        }

        public void AddLast(K key, LinkedListNode<V> node)
        {
            if (dict.ContainsKey(key))
                throw new Exception("DictionaryLinkedList already contains value.");
            linkedList.AddLast(node);
            dict[key] = node;
        }
        public void AddLast(K key, V value)
        {
            AddLast(key, new LinkedListNode<V>(value));
        }

        public LinkedListNode<V> Find(K value)
        {
            if (!dict.ContainsKey(value))
                return null;
            return dict[value];
        }

        public bool ContainsKey(K value)
        {
            return dict.ContainsKey(value);
        }

        public bool Remove(K key)
        {
            if (!dict.ContainsKey(key))
                return false;
            LinkedListNode<V> node = dict[key];
            linkedList.Remove(node);
            dict.Remove(key);
            return true;
        }

        public V RemoveCertain(K key)
        {
            if (!ContainsKey(key))
                throw new Exception($"Tried to remove an element not in the list: {key.ToString()}");
            V value = dict[key].Value;
            Remove(key);
            return value;
        }

        public void Clear()
        {
            linkedList.Clear();
            dict.Clear();
        }
    }

    /// <summary>
    /// Implementation of DictionaryLinkedList where the key and the value are the same (can look up
    /// a LinkedListNode with the value that the node contains).
    /// </summary>
    public class DictionaryLinkedList<T> : DictionaryLinkedList<T, T>, ICollection<T>
    {
        public DictionaryLinkedList()
        {
        }

        public DictionaryLinkedList(IEnumerable<T> enumerable)
        {
            foreach (T element in enumerable)
            {
                Add(element);
            }
        }

        public void AddFirst(LinkedListNode<T> node)
        {
            base.AddFirst(node.Value, node);
        }
        public void AddFirst(T value)
        {
            base.AddFirst(value, value);
        }

        public void AddLast(LinkedListNode<T> node)
        {
            base.AddLast(node.Value, node);
        }
        public void AddLast(T value)
        {
            base.AddLast(value, value);
        }

        public void AddAfter(LinkedListNode<T> node, LinkedListNode<T> newNode)
        {
            base.AddAfter(node, newNode.Value, newNode);
        }
        public void AddAfter(LinkedListNode<T> node, T value)
        {
            base.AddAfter(node, value, value);
        }
        public void AddAfter(T valueBefore, T value)
        {
            LinkedListNode<T> node = Find(valueBefore);
            base.AddAfter(node, value, value);
        }

        public void AddBefore(LinkedListNode<T> node, LinkedListNode<T> newNode)
        {
            base.AddBefore(node, newNode.Value, newNode);
        }
        public void AddBefore(LinkedListNode<T> node, T value)
        {
            base.AddBefore(node, value, value);
        }
        public void AddBefore(T valueAfter, T value)
        {
            LinkedListNode<T> node = Find(valueAfter);
            base.AddBefore(node, value, value);
        }

        public bool Remove(LinkedListNode<T> node)
        {
            if (!dict.ContainsKey(node.Value))
                return false;
            linkedList.Remove(node);
            dict.Remove(node.Value);
            return true;
        }

        // ================================================================================
        // ICollection<T> implementation
        // ================================================================================

        public bool IsReadOnly { get { return false; } }

        public void Add(T item)
        {
            AddLast(item);
        }

        public bool Contains(T value)
        {
            return Find(value) != null;
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            foreach (T value in linkedList)
            {
                array[arrayIndex++] = value;
            }
        }
    }
}
