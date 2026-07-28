using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DataApi.Tests;

[TestClass]
public sealed class MeshTreeStoreTests
{
    private static List<MeshTreeStore.DescriptorInfo> TestDescriptors =>
    [
        new(1, "Diseases", ["C"], "C"),
        new(2, "Cardiovascular Diseases", ["C14"], "C"),
        new(3, "Heart Diseases", ["C14.280"], "C"),
        new(4, "Myocardial Ischemia", ["C14.280.647"], "C"),
        new(5, "Anatomy", ["A"], "A"),
    ];

    private static Dictionary<int, int> TestCounts => new()
    {
        { 1, 10 }, { 2, 7 }, { 3, 5 }, { 4, 2 }, { 5, 0 },
    };

    private static MeshTreeStore CreateStore(TimeSpan? cacheTtl = null) =>
        cacheTtl.HasValue
            ? new MeshTreeStore("unused", cacheTtl.Value)
            : new MeshTreeStore("unused");

    private static dynamic FindNode(dynamic result, string treeNumber)
    {
        foreach (dynamic n in (IEnumerable<object>)result.nodes)
            if ((string)n.treeNumber == treeNumber)
                return n;
        return null!;
    }

    private static List<dynamic> NodesToList(dynamic result)
    {
        var list = new List<dynamic>();
        foreach (dynamic n in (IEnumerable<object>)result.nodes)
            list.Add(n);
        return list;
    }

    [TestMethod]
    public void BuildTree_RootLevel_Returns16CategoriesWithCorrectNames()
    {
        var store = CreateStore();
        store.SetTestData(TestDescriptors, TestCounts);

        dynamic result = store.BuildTree(null, 0, 0);

        Assert.AreEqual("__root__", (string)result.branch);
        var nodes = NodesToList(result);
        Assert.AreEqual(16, nodes.Count);

        dynamic aNode = FindNode(result, "A");
        Assert.AreEqual("Anatomy", (string)aNode.name);

        dynamic cNode = FindNode(result, "C");
        Assert.AreEqual("Diseases", (string)cNode.name);

        dynamic zNode = FindNode(result, "Z");
        Assert.AreEqual("Geographicals", (string)zNode.name);
    }

    [TestMethod]
    public void BuildTree_RootLevel_StudyCountsAggregatedCorrectly()
    {
        var store = CreateStore();
        store.SetTestData(TestDescriptors, TestCounts);

        dynamic result = store.BuildTree(null, 0, 0);

        dynamic cNode = FindNode(result, "C");
        Assert.AreEqual(24, (int)cNode.studyCount);

        dynamic aNode = FindNode(result, "A");
        Assert.AreEqual(0, (int)aNode.studyCount);
    }

    [TestMethod]
    public void BuildTree_RootLevel_FiltersByMinStudyCount()
    {
        var store = CreateStore();
        store.SetTestData(TestDescriptors, TestCounts);

        dynamic result = store.BuildTree(null, 5, 0);
        var nodes = NodesToList(result);

        bool hasA = false, hasC = false;
        foreach (var n in nodes) { string t = (string)n.treeNumber; if (t == "A") hasA = true; if (t == "C") hasC = true; }
        Assert.IsFalse(hasA, "A has 0 studies, should be filtered");
        Assert.IsTrue(hasC, "C has 24 studies, should be included");
    }

    [TestMethod]
    public void BuildTree_BranchLevel_ReturnsImmediateChildren()
    {
        var store = CreateStore();
        store.SetTestData(TestDescriptors, TestCounts);

        dynamic result = store.BuildTree("C", 0, 0);
        Assert.AreEqual("C", (string)result.branch);
    }

    [TestMethod]
    public void BuildTree_BranchLevel_LimitsDepth()
    {
        var store = CreateStore();
        store.SetTestData(TestDescriptors, TestCounts);

        dynamic result = store.BuildTree("C14", 0, 1);

        dynamic heartNode = FindNode(result, "C14.280");
        Assert.IsNotNull(heartNode);
        Assert.IsTrue((bool)heartNode.hasChildren);
        Assert.IsNull(heartNode.children);
    }

    [TestMethod]
    public void BuildTree_BranchLevel_NestedChildrenAtDepth()
    {
        var store = CreateStore();
        store.SetTestData(TestDescriptors, TestCounts);

        dynamic result = store.BuildTree("C14", 0, 2);

        dynamic heartNode = FindNode(result, "C14.280");
        Assert.IsNotNull(heartNode);

        var children = new List<dynamic>();
        foreach (dynamic c in (IEnumerable<object>)heartNode.children)
            children.Add(c);

        dynamic ischemiaNode = null!;
        foreach (var c in children)
            if ((string)c.treeNumber == "C14.280.647") { ischemiaNode = c; break; }
        Assert.IsNotNull(ischemiaNode);
        Assert.AreEqual(2, (int)ischemiaNode.studyCount);
    }

    [TestMethod]
    public void BuildTree_BranchLevel_FiltersChildrenByStudyCount()
    {
        var store = CreateStore();
        store.SetTestData(TestDescriptors, TestCounts);

        dynamic result = store.BuildTree("C14", 8, 1);

        dynamic heartNode = FindNode(result, "C14.280");
        Assert.IsNull(heartNode);
    }

    [TestMethod]
    public void GetOrBuildTree_ColdMiss_CallsBuilderAndCaches()
    {
        var store = CreateStore();
        int callCount = 0;

        var result = store.GetOrBuildTree("test", () => { callCount++; return "value1"; });

        Assert.AreEqual("value1", result);
        Assert.AreEqual(1, callCount);
    }

    [TestMethod]
    public void GetOrBuildTree_WarmHit_ReturnsCached_DoesNotRebuild()
    {
        var store = CreateStore();
        int callCount = 0;

        var first = store.GetOrBuildTree("test", () => { callCount++; return "value1"; });
        var second = store.GetOrBuildTree("test", () => { callCount++; return "value2"; });

        Assert.AreEqual("value1", first);
        Assert.AreEqual("value1", second);
        Assert.AreEqual(1, callCount);
    }

    [TestMethod]
    public void GetOrBuildTree_DifferentKeys_Independent()
    {
        var store = CreateStore();

        var a = store.GetOrBuildTree("a", () => "result_a");
        var b = store.GetOrBuildTree("b", () => "result_b");

        Assert.AreEqual("result_a", a);
        Assert.AreEqual("result_b", b);
    }

    [TestMethod]
    public async Task GetOrBuildTree_Stale_ReturnsPreviousWhileRebuilding()
    {
        var store = CreateStore(cacheTtl: TimeSpan.FromMilliseconds(50));
        int callCount = 0;

        var v1 = store.GetOrBuildTree("test", () => { callCount++; return "v1"; });
        Assert.AreEqual("v1", v1);
        Assert.AreEqual(1, callCount);

        await Task.Delay(100).ConfigureAwait(false);

        using var refreshStarted = new ManualResetEventSlim(false);
        var stale = store.GetOrBuildTree("test", () =>
        {
            callCount++;
            refreshStarted.Set();
            Thread.Sleep(1000);
            return "v2";
        });

        Assert.AreEqual("v1", stale);

        Assert.IsTrue(refreshStarted.Wait(2000));

        var duringRefresh = store.GetOrBuildTree("test", () => { callCount++; return "v3"; });
        Assert.AreEqual("v1", duringRefresh);
        Assert.AreEqual(2, callCount);

        await Task.Delay(1500).ConfigureAwait(false);

        var fresh = store.GetOrBuildTree("test", () => { callCount++; return "v4"; });
        Assert.AreEqual("v2", fresh);
        Assert.AreEqual(2, callCount);
    }
}
