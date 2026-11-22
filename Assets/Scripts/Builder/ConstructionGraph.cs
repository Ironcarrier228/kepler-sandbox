using System;
using System.Collections.Generic;
using Kepler.Parts;
using UnityEngine;

namespace Kepler.Builder
{
    [Serializable]
    public sealed class PlacedPart
    {
        public string instanceId, definitionId, parentId, parentNode, childNode;
        public Vector3 position;
        public Quaternion rotation = Quaternion.identity;
    }

    [Serializable]
    public sealed class ConstructionDocument
    {
        public int version = 1;
        public List<PlacedPart> parts = new List<PlacedPart>();
    }

    // Scene-independent graph. Nodes are single-use and every non-root part has one parent.
    // Stored poses are recalculated from attachments on load, not trusted from the JSON.
    public sealed class ConstructionGraph
    {
        public const int MaxParts = 256;
        readonly Dictionary<string, TankDefinition> catalog = new Dictionary<string, TankDefinition>();
        readonly Stack<string> undo = new Stack<string>();
        readonly Stack<string> redo = new Stack<string>();
        ConstructionDocument document = new ConstructionDocument();
        public bool CanUndo => undo.Count > 0;
        public bool CanRedo => redo.Count > 0;
        public int Count => document.parts.Count;
        public ConstructionGraph(IEnumerable<TankDefinition> definitions)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            foreach (var definition in definitions)
            {
                if (!definition || string.IsNullOrWhiteSpace(definition.id) || !definition.prefab || catalog.ContainsKey(definition.id))
                    throw new ArgumentException("Invalid or duplicate part definition");
                catalog.Add(definition.id, definition);
            }
        }
        public string ToJson() => JsonUtility.ToJson(document, true);
        public ConstructionDocument Snapshot() => JsonUtility.FromJson<ConstructionDocument>(ToJson());
        TankDefinition Definition(string id) => id != null && catalog.TryGetValue(id, out var def) ? def : null;
        static AttachNode Node(TankDefinition definition, string name)
        {
            if (!definition || !definition.prefab || string.IsNullOrEmpty(name)) return null;
            foreach (var node in definition.prefab.GetComponentsInChildren<AttachNode>(true))
                if (node.nodeId == name) return node;
            return null;
        }
        static PlacedPart Part(ConstructionDocument graph, string id)
        {
            foreach (var part in graph.parts) if (part.instanceId == id) return part;
            return null;
        }
        static bool Occupied(ConstructionDocument graph, string partId, string nodeId)
        {
            foreach (var p in graph.parts)
                if ((p.parentId == partId && p.parentNode == nodeId) ||
                    (p.instanceId == partId && !string.IsNullOrEmpty(p.parentId) && p.childNode == nodeId)) return true;
            return false;
        }
        static void Align(PlacedPart parent, AttachNode parentNode, PlacedPart child, AttachNode childNode)
        {
            // Attach nodes in generated prefabs are direct children. Their +Y axes face outwards.
            Quaternion anchorRotation = parent.rotation * parentNode.transform.localRotation;
            child.rotation = anchorRotation * Quaternion.Euler(180, 0, 0) * Quaternion.Inverse(childNode.transform.localRotation);
            Vector3 anchorPosition = parent.position + parent.rotation * parentNode.transform.localPosition;
            child.position = anchorPosition - child.rotation * childNode.transform.localPosition;
        }
        void Checkpoint()
        {
            undo.Push(ToJson());
            redo.Clear();
        }
        public bool AddRoot(string definitionId)
        {
            if (Count != 0 || !Definition(definitionId)) return false;
            Checkpoint();
            document.parts.Add(new PlacedPart { instanceId = Guid.NewGuid().ToString("N"), definitionId = definitionId });
            return true;
        }
        public bool Attach(string parentId, string parentNodeId, string definitionId, string childNodeId)
        {
            var parent = Part(document, parentId);
            var parentDefinition = parent == null ? null : Definition(parent.definitionId);
            var childDefinition = Definition(definitionId);
            var a = Node(parentDefinition, parentNodeId);
            var b = Node(childDefinition, childNodeId);
            if (Count >= MaxParts || parent == null || !a || !b || a.size != b.size || Occupied(document, parentId, parentNodeId)) return false;
            var child = new PlacedPart { instanceId = Guid.NewGuid().ToString("N"), definitionId = definitionId,
                                         parentId = parentId, parentNode = parentNodeId, childNode = childNodeId };
            Align(parent, a, child, b);
            Checkpoint();
            document.parts.Add(child);
            return true;
        }
        public bool Remove(string id)
        {
            if (Part(document, id) == null) return false;
            var removed = new HashSet<string> { id };
            bool changed;
            do
            {
                changed = false;
                foreach (var p in document.parts)
                    if (!string.IsNullOrEmpty(p.parentId) && removed.Contains(p.parentId)) changed |= removed.Add(p.instanceId);
            } while (changed);
            Checkpoint();
            document.parts.RemoveAll(p => removed.Contains(p.instanceId));
            return true;
        }
        // Loads are transactional: malformed or incompatible documents cannot change the current graph.
        public bool LoadJson(string json)
        {
            if (!TryParse(json, out var candidate)) return false;
            Checkpoint();
            document = candidate;
            return true;
        }
        bool TryParse(string json, out ConstructionDocument result)
        {
            result = null;
            if (string.IsNullOrWhiteSpace(json) || json.Length > 1024 * 1024) return false;
            ConstructionDocument input;
            try { input = JsonUtility.FromJson<ConstructionDocument>(json); }
            catch (ArgumentException) { return false; }
            if (input == null || input.version != 1 || input.parts == null || input.parts.Count > MaxParts) return false;
            var built = new ConstructionDocument();
            var ids = new HashSet<string>();
            foreach (var item in input.parts)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.instanceId) || !ids.Add(item.instanceId) || !Definition(item.definitionId)) return false;
                // Canonical format requires parent-before-child, preventing cycles and orphan nodes.
                if (built.parts.Count == 0)
                {
                    if (!string.IsNullOrEmpty(item.parentId)) return false;
                    item.parentId = item.parentNode = item.childNode = null;
                    item.position = Vector3.zero; item.rotation = Quaternion.identity;
                }
                else
                {
                    var parent = Part(built, item.parentId);
                    var a = parent == null ? null : Node(Definition(parent.definitionId), item.parentNode);
                    var b = Node(Definition(item.definitionId), item.childNode);
                    if (parent == null || !a || !b || a.size != b.size || Occupied(built, item.parentId, item.parentNode)) return false;
                    Align(parent, a, item, b);
                }
                built.parts.Add(item);
            }
            result = built;
            return true;
        }
        public bool Undo()
        {
            if (undo.Count == 0) return false;
            string before = undo.Pop();
            if (!TryParse(before, out var restored)) throw new InvalidOperationException("Invalid undo state");
            redo.Push(ToJson()); document = restored; return true;
        }
        public bool Redo()
        {
            if (redo.Count == 0) return false;
            string after = redo.Pop();
            if (!TryParse(after, out var restored)) throw new InvalidOperationException("Invalid redo state");
            undo.Push(ToJson()); document = restored; return true;
        }
    }
}
