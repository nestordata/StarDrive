#include "SdMaterial.h"
#include "SdMesh.h"

namespace SdMesh
{
    DLLAPI(SDMaterial*) SDMeshCreateMaterial(
        SDMesh* mesh,
        const sd_wchar* name, 
        const sd_wchar* diffusePath, 
        const sd_wchar* alphaPath, 
        const sd_wchar* specularPath, 
        const sd_wchar* normalPath, 
        const sd_wchar* emissivePath, 
        Color3 ambientColor, 
        Color3 diffuseColor, 
        Color3 specularColor, 
        Color3 emissiveColor, 
        float specular, 
        float alpha)
    {
        shared_ptr<Nano::Material> matPtr = std::make_shared<Nano::Material>();
        Nano::Material& mat = *matPtr;
        mat.Name          = toString(name);
        mat.DiffusePath   = toString(diffusePath);
        mat.AlphaPath     = toString(alphaPath);
        mat.SpecularPath  = toString(specularPath);
        mat.NormalPath    = toString(normalPath);
        mat.EmissivePath  = toString(emissivePath);
        mat.AmbientColor  = ambientColor;
        mat.DiffuseColor  = diffuseColor;
        mat.SpecularColor = specularColor;
        mat.EmissiveColor = emissiveColor;
        mat.Specular      = specular;
        mat.Alpha         = alpha;
        return mesh->GetOrCreateMat(matPtr);
    }

    DLLAPI(void) SDMeshGroupSetMaterial(SDMeshGroup* group, SDMaterial* material)
    {
        group->Mat = material;
        group->GetGroup().Mat = material ? material->Mat : nullptr;
    }

    
}
