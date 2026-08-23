using System.Collections.Generic;
using System.Reflection;
using DialogSystem.EditorTools.View.Elements;
using NUnit.Framework;
using UnityEngine;

namespace DialogSystem.Tests.EditMode
{
    public sealed class DialogGraphEdgeRouteTests
    {
        private static readonly MethodInfo BuildRenderRoutePointsMethod = typeof(DialogGraphEdge).GetMethod(
            "BuildRenderRoutePoints",
            BindingFlags.NonPublic | BindingFlags.Static);

        private static readonly MethodInfo GetSerializedInsertionIndexMethod = typeof(DialogGraphEdge).GetMethod(
            "GetSerializedInsertionIndex",
            BindingFlags.NonPublic | BindingFlags.Static);

        private static readonly float PortStubLength = (float)typeof(DialogGraphEdge)
            .GetField("PortStubLength", BindingFlags.NonPublic | BindingFlags.Static)
            .GetRawConstantValue();

        [Test]
        public void BuildRenderRoutePoints_WithUserReroutes_AddsHiddenSourceAndTargetStubs()
        {
            var serializedUserReroutePoints = new List<Vector2>
            {
                new(180f, 120f),
                new(260f, 200f)
            };
            var renderRoutePoints = new List<Vector2>();
            var sourcePortCenter = new Vector2(100f, 100f);
            var targetPortCenter = new Vector2(400f, 220f);

            BuildRenderRoutePointsMethod.Invoke(null, new object[]
            {
                sourcePortCenter,
                new Vector2(sourcePortCenter.x + PortStubLength, sourcePortCenter.y),
                serializedUserReroutePoints,
                new Vector2(targetPortCenter.x - PortStubLength, targetPortCenter.y),
                targetPortCenter,
                renderRoutePoints
            });

            Assert.That(renderRoutePoints.Count, Is.EqualTo(6));
            Assert.That(renderRoutePoints[0], Is.EqualTo(sourcePortCenter));
            Assert.That(renderRoutePoints[1], Is.EqualTo(new Vector2(sourcePortCenter.x + PortStubLength, sourcePortCenter.y)));
            Assert.That(renderRoutePoints[2], Is.EqualTo(serializedUserReroutePoints[0]));
            Assert.That(renderRoutePoints[3], Is.EqualTo(serializedUserReroutePoints[1]));
            Assert.That(renderRoutePoints[4], Is.EqualTo(new Vector2(targetPortCenter.x - PortStubLength, targetPortCenter.y)));
            Assert.That(renderRoutePoints[5], Is.EqualTo(targetPortCenter));
            Assert.That(serializedUserReroutePoints.Count, Is.EqualTo(2));
            Assert.That(serializedUserReroutePoints[0], Is.EqualTo(new Vector2(180f, 120f)));
            Assert.That(serializedUserReroutePoints[1], Is.EqualTo(new Vector2(260f, 200f)));
        }

        [Test]
        public void BuildRenderRoutePoints_WithoutUserReroutes_KeepsDirectPortToPortRoute()
        {
            var renderRoutePoints = new List<Vector2>();
            var sourcePortCenter = new Vector2(32f, 48f);
            var targetPortCenter = new Vector2(192f, 96f);

            BuildRenderRoutePointsMethod.Invoke(null, new object[]
            {
                sourcePortCenter,
                new Vector2(sourcePortCenter.x + PortStubLength, sourcePortCenter.y),
                null,
                new Vector2(targetPortCenter.x - PortStubLength, targetPortCenter.y),
                targetPortCenter,
                renderRoutePoints
            });

            Assert.That(renderRoutePoints.Count, Is.EqualTo(2));
            Assert.That(renderRoutePoints[0], Is.EqualTo(sourcePortCenter));
            Assert.That(renderRoutePoints[1], Is.EqualTo(targetPortCenter));
        }

        [Test]
        public void GetSerializedInsertionIndex_MapsRenderSegmentsBackToVisibleRerouteIndices()
        {
            Assert.That(GetSerializedInsertionIndex(0, 2), Is.EqualTo(0));
            Assert.That(GetSerializedInsertionIndex(1, 2), Is.EqualTo(0));
            Assert.That(GetSerializedInsertionIndex(2, 2), Is.EqualTo(1));
            Assert.That(GetSerializedInsertionIndex(3, 2), Is.EqualTo(2));
        }

        private static int GetSerializedInsertionIndex(int renderSegmentIndex, int serializedReroutePointCount)
        {
            return (int)GetSerializedInsertionIndexMethod.Invoke(null, new object[] { renderSegmentIndex, serializedReroutePointCount });
        }
    }
}
