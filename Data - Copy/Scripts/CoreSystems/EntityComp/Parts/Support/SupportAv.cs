using Sandbox.Game.Entities;

namespace CoreSystems.Platform
{
    public partial class SupportSys
    {
        internal void ToggleAreaEffectDisplay()
        {
            var grid = BaseComp.Cube.CubeGrid;
            if (!ShowAffectedBlocks) {

                ShowAffectedBlocks = true;
                foreach (var slim in SuppotedBlocks)
                {
                    if (!slim.IsDestroyed)
                    {
                        MyCube myCube;
                        Comp.Cube.CubeGrid.TryGetCube(slim.Position, out myCube);
                        BlockColorBackup.Add(slim, new BlockBackup { MyCube = myCube, OriginalColor = slim.ColorMaskHSV, OriginalSkin = slim.SkinSubtypeId });
                    }
                }

                CoreSystem.Session.DisplayAffectedArmor.Add(this);
            }
            else {

                foreach (var pair in BlockColorBackup)
                {
                    if (!pair.Key.IsDestroyed)
                        grid.ChangeColorAndSkin(pair.Value.MyCube.CubeBlock, pair.Value.OriginalColor, pair.Value.OriginalSkin);
                }

                BlockColorBackup.Clear();
                CoreSystem.Session.DisplayAffectedArmor.Remove(this);
                ShowAffectedBlocks = false;
            }
        }

    }
}
