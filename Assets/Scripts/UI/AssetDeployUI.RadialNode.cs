using System;
using System.Collections.Generic;
using ByteWar.Building;

namespace ByteWar.UI
{
    public sealed partial class AssetDeployUI
    {
        private sealed class RadialNode
        {
            public readonly string DisplayName;
            public readonly RadialLeafKind LeafKind;

            public RadialNode Parent { get; private set; }
            public readonly List<RadialNode> Children = new();

            public DeployableAssetCatalog.Entry CatalogEntry;
            public BuildingPieceType BuildingPieceType;
            public int PreferredRecipeIndex = -1;

            public RadialNode(string displayName, RadialLeafKind leafKind)
            {
                DisplayName = displayName;
                LeafKind = leafKind;
            }

            public static RadialNode CreateDeveloperLeaf(string displayName, DeployableAssetCatalog.Entry entry)
            {
                return new RadialNode(displayName, RadialLeafKind.DeveloperAsset)
                {
                    CatalogEntry = entry
                };
            }

            public static RadialNode CreateBuildingLeaf(string displayName, BuildingPieceType pieceType, int preferredRecipeIndex)
            {
                return new RadialNode(displayName, RadialLeafKind.BuildingRecipe)
                {
                    BuildingPieceType = pieceType,
                    PreferredRecipeIndex = preferredRecipeIndex,
                };
            }

            public void AddChild(RadialNode child)
            {
                if (child == null)
                    return;

                child.Parent = this;
                Children.Add(child);
            }

            public RadialNode GetOrCreateChild(string displayName)
            {
                for (int i = 0; i < Children.Count; i++)
                {
                    if (Children[i].LeafKind != RadialLeafKind.None)
                        continue;

                    if (string.Equals(Children[i].DisplayName, displayName, StringComparison.OrdinalIgnoreCase))
                        return Children[i];
                }

                var created = new RadialNode(displayName, RadialLeafKind.None);
                AddChild(created);
                return created;
            }

            public void SortRecursive()
            {
                Children.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));

                for (int i = 0; i < Children.Count; i++)
                    Children[i].SortRecursive();
            }

            public RadialNode FindFirstLeaf()
            {
                if (LeafKind != RadialLeafKind.None)
                    return this;

                for (int i = 0; i < Children.Count; i++)
                {
                    var found = Children[i].FindFirstLeaf();
                    if (found != null)
                        return found;
                }

                return null;
            }
        }
    }
}
