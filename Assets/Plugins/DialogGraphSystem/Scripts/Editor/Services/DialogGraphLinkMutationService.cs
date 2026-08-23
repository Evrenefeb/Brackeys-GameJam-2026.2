using System;
using System.Collections.Generic;
using System.Linq;
using DialogSystem.Runtime.Models;

namespace DialogSystem.EditorTools.Services
{
    /// <summary>
    /// Mutation boundary for serialized dialog graph links.
    /// </summary>
    public interface IDialogGraphLinkMutationService
    {
        /// <summary>
        /// Finds the first link matching source node, destination node, and source output port.
        /// </summary>
        GraphLink FindExactLink(DialogGraph graph, string fromGuid, string toGuid, int fromPortIndex, string fromPortKey = null);

        /// <summary>
        /// Finds all links that originate from a specific source node output port.
        /// </summary>
        List<GraphLink> FindLinksFromPort(DialogGraph graph, string fromGuid, int fromPortIndex, string fromPortKey = null);

        /// <summary>
        /// Adds a new link for an output port or replaces that port's existing target.
        /// </summary>
        GraphLink AddOrReplaceLink(
            DialogGraph graph,
            string fromGuid,
            string toGuid,
            int fromPortIndex,
            string fromPortKey = null,
            string toPortKey = null);

        /// <summary>
        /// Removes links matching source node, destination node, and source output port.
        /// </summary>
        bool RemoveExactLink(DialogGraph graph, string fromGuid, string toGuid, int fromPortIndex, string fromPortKey = null);

        /// <summary>
        /// Removes every link from a specific source output port.
        /// </summary>
        int RemoveLinksFromPort(DialogGraph graph, string fromGuid, int fromPortIndex, string fromPortKey = null);

        /// <summary>
        /// Removes every serialized link that starts or ends at the specified node.
        /// </summary>
        int RemoveLinksConnectedToNode(DialogGraph graph, string nodeGuid);
    }

    /// <summary>
    /// Editor-only mutation boundary for serialized graph links.
    /// </summary>
    public sealed class DefaultDialogGraphLinkMutationService : IDialogGraphLinkMutationService
    {
        #region ---------------- Public API ----------------

        /// <summary>
        /// Finds the first link matching source node, destination node, and source output port.
        /// </summary>
        public GraphLink FindExactLink(DialogGraph graph, string fromGuid, string toGuid, int fromPortIndex, string fromPortKey = null)
        {
            if (graph?.links == null || !HasValidEndpointIdentity(fromGuid, toGuid))
            {
                return null;
            }

            return graph.links.FirstOrDefault(link => IsExactLink(link, fromGuid, toGuid, fromPortIndex, fromPortKey));
        }

        /// <summary>
        /// Finds all links that originate from a specific source node output port.
        /// </summary>
        public List<GraphLink> FindLinksFromPort(DialogGraph graph, string fromGuid, int fromPortIndex, string fromPortKey = null)
        {
            if (graph?.links == null || string.IsNullOrWhiteSpace(fromGuid))
            {
                return new List<GraphLink>();
            }

            return graph.links
                .Where(link => IsFromPort(link, fromGuid, fromPortIndex, fromPortKey))
                .ToList();
        }

        /// <summary>
        /// Adds a new link for an output port or replaces that port's existing target.
        /// </summary>
        public GraphLink AddOrReplaceLink(
            DialogGraph graph,
            string fromGuid,
            string toGuid,
            int fromPortIndex,
            string fromPortKey = null,
            string toPortKey = null)
        {
            if (graph == null)
            {
                throw new ArgumentNullException(nameof(graph));
            }

            if (!HasValidEndpointIdentity(fromGuid, toGuid))
            {
                return null;
            }

            graph.links ??= new List<GraphLink>();

            var linksFromPort = FindLinksFromPort(graph, fromGuid, fromPortIndex, fromPortKey);
            var exactLinks = linksFromPort
                .Where(link => IsExactLink(link, fromGuid, toGuid, fromPortIndex, fromPortKey))
                .ToList();

            if (exactLinks.Count > 0)
            {
                var preservedLink = exactLinks.FirstOrDefault(link => link.HasLinkGuid) ?? exactLinks[0];
                StampPortKeys(preservedLink, fromPortKey, toPortKey);
                RemoveLinksFromPortExcept(graph, fromGuid, fromPortIndex, fromPortKey, preservedLink);
                return preservedLink;
            }

            RemoveLinksFromPort(graph, fromGuid, fromPortIndex, fromPortKey);

            var newLink = new GraphLink
            {
                fromGuid = fromGuid,
                toGuid = toGuid,
                fromPortKey = NormalizePortKey(fromPortKey),
                toPortKey = NormalizePortKey(toPortKey),
                fromPortIndex = fromPortIndex
            };

            newLink.AssignLinkGuidIfMissing(CreateLinkGuid());
            graph.links.Add(newLink);
            return newLink;
        }

        /// <summary>
        /// Removes links matching source node, destination node, and source output port.
        /// </summary>
        public bool RemoveExactLink(DialogGraph graph, string fromGuid, string toGuid, int fromPortIndex, string fromPortKey = null)
        {
            if (graph?.links == null || !HasValidEndpointIdentity(fromGuid, toGuid))
            {
                return false;
            }

            return graph.links.RemoveAll(link => IsExactLink(link, fromGuid, toGuid, fromPortIndex, fromPortKey)) > 0;
        }

        /// <summary>
        /// Removes every link from a specific source output port.
        /// </summary>
        public int RemoveLinksFromPort(DialogGraph graph, string fromGuid, int fromPortIndex, string fromPortKey = null)
        {
            if (graph?.links == null || string.IsNullOrWhiteSpace(fromGuid))
            {
                return 0;
            }

            return graph.links.RemoveAll(link => IsFromPort(link, fromGuid, fromPortIndex, fromPortKey));
        }

        /// <summary>
        /// Removes every serialized link that starts or ends at the specified node.
        /// </summary>
        public int RemoveLinksConnectedToNode(DialogGraph graph, string nodeGuid)
        {
            if (graph?.links == null || string.IsNullOrWhiteSpace(nodeGuid))
            {
                return 0;
            }

            return graph.links.RemoveAll(link =>
                link != null &&
                (string.Equals(link.fromGuid, nodeGuid, StringComparison.Ordinal) ||
                 string.Equals(link.toGuid, nodeGuid, StringComparison.Ordinal)));
        }

        #endregion

        #region ---------------- Matching ----------------

        private static void RemoveLinksFromPortExcept(
            DialogGraph graph,
            string fromGuid,
            int fromPortIndex,
            string fromPortKey,
            GraphLink preservedLink)
        {
            graph.links.RemoveAll(link =>
                IsFromPort(link, fromGuid, fromPortIndex, fromPortKey) &&
                !ReferenceEquals(link, preservedLink));
        }

        private static bool IsExactLink(GraphLink link, string fromGuid, string toGuid, int fromPortIndex, string fromPortKey)
        {
            return IsFromPort(link, fromGuid, fromPortIndex, fromPortKey) &&
                   string.Equals(link.toGuid, toGuid, StringComparison.Ordinal);
        }

        private static bool IsFromPort(GraphLink link, string fromGuid, int fromPortIndex, string fromPortKey)
        {
            if (link == null ||
                !string.Equals(link.fromGuid, fromGuid, StringComparison.Ordinal))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(fromPortKey) &&
                !string.IsNullOrWhiteSpace(link.fromPortKey))
            {
                return string.Equals(link.fromPortKey, fromPortKey, StringComparison.Ordinal);
            }

            return link.fromPortIndex == fromPortIndex;
        }

        private static void StampPortKeys(GraphLink link, string fromPortKey, string toPortKey)
        {
            if (link == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(fromPortKey))
            {
                link.fromPortKey = fromPortKey;
            }

            if (!string.IsNullOrWhiteSpace(toPortKey))
            {
                link.toPortKey = toPortKey;
            }
        }

        private static string NormalizePortKey(string portKey)
        {
            return string.IsNullOrWhiteSpace(portKey) ? string.Empty : portKey;
        }

        private static bool HasValidEndpointIdentity(string fromGuid, string toGuid)
        {
            return !string.IsNullOrWhiteSpace(fromGuid) &&
                   !string.IsNullOrWhiteSpace(toGuid);
        }

        private static string CreateLinkGuid()
        {
            return Guid.NewGuid().ToString("N");
        }

        #endregion
    }

    /// <summary>
    /// Backward-compatible static entry point for serialized graph link mutations.
    /// </summary>
    public static class DialogGraphLinkMutationService
    {
        private static readonly IDialogGraphLinkMutationService DefaultService = new DefaultDialogGraphLinkMutationService();

        /// <summary>
        /// Finds the first link matching source node, destination node, and source output port.
        /// </summary>
        public static GraphLink FindExactLink(DialogGraph graph, string fromGuid, string toGuid, int fromPortIndex, string fromPortKey = null)
        {
            return DefaultService.FindExactLink(graph, fromGuid, toGuid, fromPortIndex, fromPortKey);
        }

        /// <summary>
        /// Finds all links that originate from a specific source node output port.
        /// </summary>
        public static List<GraphLink> FindLinksFromPort(DialogGraph graph, string fromGuid, int fromPortIndex, string fromPortKey = null)
        {
            return DefaultService.FindLinksFromPort(graph, fromGuid, fromPortIndex, fromPortKey);
        }

        /// <summary>
        /// Adds a new link for an output port or replaces that port's existing target.
        /// </summary>
        public static GraphLink AddOrReplaceLink(
            DialogGraph graph,
            string fromGuid,
            string toGuid,
            int fromPortIndex,
            string fromPortKey = null,
            string toPortKey = null)
        {
            return DefaultService.AddOrReplaceLink(graph, fromGuid, toGuid, fromPortIndex, fromPortKey, toPortKey);
        }

        /// <summary>
        /// Removes links matching source node, destination node, and source output port.
        /// </summary>
        public static bool RemoveExactLink(DialogGraph graph, string fromGuid, string toGuid, int fromPortIndex, string fromPortKey = null)
        {
            return DefaultService.RemoveExactLink(graph, fromGuid, toGuid, fromPortIndex, fromPortKey);
        }

        /// <summary>
        /// Removes every link from a specific source output port.
        /// </summary>
        public static int RemoveLinksFromPort(DialogGraph graph, string fromGuid, int fromPortIndex, string fromPortKey = null)
        {
            return DefaultService.RemoveLinksFromPort(graph, fromGuid, fromPortIndex, fromPortKey);
        }

        /// <summary>
        /// Removes every serialized link that starts or ends at the specified node.
        /// </summary>
        public static int RemoveLinksConnectedToNode(DialogGraph graph, string nodeGuid)
        {
            return DefaultService.RemoveLinksConnectedToNode(graph, nodeGuid);
        }
    }
}
