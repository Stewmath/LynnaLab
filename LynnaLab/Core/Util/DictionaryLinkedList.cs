using System;
using System.Collections;
using System.Collections.Generic;

namespace LynnaLab
{
    // Like LinkedList<T>, but lookups (for adds/removals/finds) can be done in O(1)-ish time like
    // a Dictionary. Can't have the same value twice as a consequence.
    public class DictionaryLinkedList<T> : IEnumerable<T>
    {
        LinkedList<T> linkedList = new LinkedList<T>();
        Dictionary<T, LinkedListNode<T>> dict = new Dictionary<T, LinkedListNode<T>>();


        public int Count { get { return linkedList.Count; } }

        public LinkedListNode<T> First { get { return linkedList.First; } }
        public LinkedListNode<T> Last  { get { return linkedList.Last; } }

        public DictionaryLinkedList() : base() {
        }

        public void AddAfter(LinkedListNode<T> node, LinkedListNode<T> newNode) {
            if (dict.ContainsKey(newNode.Value))
                throw new Exception("DictionaryLinkedList already contains value.");
            linkedList.AddAfter(node, newNode);
            dict[newNode.Value] = newNode;
        }
        public void AddAfter(LinkedListNode<T> node, T newNode) {
            AddAfter(node, new LinkedListNode<T>(newNode));
        }
        public void AddAfter(T node, T newNode) {
            AddAfter(dict[node], newNode);
        }

        public void AddBefore(LinkedListNode<T> node, LinkedListNode<T> newNode) {
            if (dict.ContainsKey(newNode.Value))
                throw new Exception("DictionaryLinkedList already contains value.");
            linkedList.AddBefore(node, newNode);
            dict[newNode.Value] = newNode;
        }
        public void AddBefore(LinkedListNode<T> node, T newNode) {
            AddBefore(node, new LinkedListNode<T>(newNode));
        }
        public void AddBefore(T node, T newNode) {
            AddBefore(dict[node], newNode);
        }

        public void AddFirst(LinkedListNode<T> node) {
            if (dict.ContainsKey(node.Value))
                throw new Exception("DictionaryLinkedList already contains value.");
            linkedList.AddFirst(node);
            dict[node.Value] = node;
        }
        public void AddFirst(T value) {
            AddFirst(new LinkedListNode<T>(value));
        }

        public void AddLast(LinkedListNode<T> node) {
            if (dict.ContainsKey(node.Value))
                throw new Exception("DictionaryLinkedList already contains value.");
            linkedList.AddLast(node);
            dict[node.Value] = node;
        }
        public void AddLast(T value) {
            AddLast(new LinkedListNode<T>(value));
        }

        public LinkedListNode<T> Find(T value) {
            if (!dict.ContainsKey(value))
                return null;
            return dict[value];
        }

        public void Remove(LinkedListNode<T> node) {
            linkedList.Remove(node);
            dict.Remove(node.Value);
        }
        public void Remove(T value) {
            Remove(dict[value]);
        }

        public bool Contains(T value) {
            return dict.ContainsKey(value);
        }


        // IEnumerable<T> implementation
        public IEnumerator<T> GetEnumerator() {
            return linkedList.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return linkedList.GetEnumerator();
        }
    }
}
