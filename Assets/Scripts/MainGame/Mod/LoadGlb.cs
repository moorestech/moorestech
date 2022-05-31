using System;
using System.Threading.Tasks;
using UniGLTF;

namespace MainGame.Mod
{
    public class LoadGlb
    {
        async Task<RuntimeGltfInstance> LoadAsync(string path)
        {
            GltfData data = new AutoGltfFileParser(path).Parse();

            // doMigrate: true で旧バージョンの vrm をロードできます。
            if (Vrm10Data.TryParseOrMigrate(data, doMigrate: true, out Vrm10Data vrm))
            {
                // vrm
                using (var loader = new Vrm10Importer(vrm,
                           materialGenerator: GetVrmMaterialDescriptorGenerator(m_useUrpMaterial.isOn)))
                {
                    // migrate しても thumbnail は同じ
                    var thumbnail = await loader.LoadVrmThumbnailAsync();

                    if (vrm.OriginalMetaBeforeMigration != null)
                    {
                        // migrated from vrm-0.x. use OriginalMetaBeforeMigration
                        UpdateMeta(vrm.OriginalMetaBeforeMigration, thumbnail);
                    }
                    else
                    {
                        // load vrm-1.0. use newMeta
                        UpdateMeta(vrm.VrmExtension.Meta, thumbnail);
                    }

                    // モデルをロード
                    RuntimeGltfInstance instance = await loader.LoadAsync();
                    return instance;
                }
            }
            else
            {
                throw new Exception("not vrm");
            }
        }
    }
}