namespace Frontend.Pages;

public class MeshTreeNode
{
    public string TreeNumber { get; set; } = "";
    public string Name { get; set; } = "";
    public int StudyCount { get; set; }
    public bool HasChildren { get; set; }
    public List<MeshTreeNode>? Children { get; set; }
}

public class MeshTreeBranchResponse
{
    public string? Branch { get; set; }
    public List<MeshTreeBranchNode>? Nodes { get; set; }
}

public class MeshTreeBranchNode
{
    public string TreeNumber { get; set; } = "";
    public string Name { get; set; } = "";
    public int StudyCount { get; set; }
    public bool HasChildren { get; set; }
    public List<MeshTreeBranchNode>? Children { get; set; }
}
