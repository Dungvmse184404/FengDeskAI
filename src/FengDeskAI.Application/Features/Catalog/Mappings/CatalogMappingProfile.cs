using AutoMapper;
using FengDeskAI.Application.Features.Catalog.DTOs;
using FengDeskAI.Domain.Entities.Catalog;
using FengDeskAI.Domain.Enums.Catalog;

namespace FengDeskAI.Application.Features.Catalog.Mappings;

public class CatalogMappingProfile : Profile
{
    public CatalogMappingProfile()
    {
        // Category
        CreateMap<Category, CategoryResponse>();
        CreateMap<Category, CategoryRefResponse>();
        CreateMap<CreateCategoryRequest, Category>()
            .ForMember(d => d.Id, o => o.Ignore())
            .ForMember(d => d.IsActive, o => o.Ignore())
            .ForMember(d => d.Parent, o => o.Ignore())
            .ForMember(d => d.Children, o => o.Ignore());

        // Tag (CRUD độc lập — đã ngừng dùng ở luồng product)
        CreateMap<Tag, TagResponse>();
        CreateMap<CreateTagRequest, Tag>().ForMember(d => d.Id, o => o.Ignore());

        // Product items & images
        CreateMap<ProductItem, ProductItemResponse>();
        CreateMap<ProductImage, ProductImageResponse>();

        // Model 3D — Status enum → tên chuỗi. IsEnabled tự map theo tên (convention).
        CreateMap<ProductModel3D, ProductModel3DResponse>()
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()));

        // Model3DRequest — owner/garden staff (che giấu lý do hết credit, hiện "Processing" thay vì
        // lộ InternalFailureReason) vs staff sàn (đầy đủ, kèm tên product/store).
        CreateMap<Model3DRequest, Model3DRequestResponse>()
            .ForMember(d => d.RequestType, o => o.MapFrom(s => s.RequestType.ToString()))
            .ForMember(d => d.Status, o => o.MapFrom(s =>
                s.InternalFailureReason == Model3DFailureReason.InsufficientCredits
                    ? Model3DRequestStatus.Processing.ToString()
                    : s.Status.ToString()));

        CreateMap<Model3DRequest, Model3DRequestQueueItemResponse>()
            .ForMember(d => d.ProductName, o => o.MapFrom(s => s.Product.Name))
            .ForMember(d => d.StoreName, o => o.MapFrom(s => s.Product.Store.Name))
            .ForMember(d => d.ProductImageUrl, o => o.MapFrom(s => s.ProductImage != null ? s.ProductImage.Url : null))
            .ForMember(d => d.RequestType, o => o.MapFrom(s => s.RequestType.ToString()))
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()))
            .ForMember(d => d.InternalFailureReason, o => o.MapFrom(s => s.InternalFailureReason != null ? s.InternalFailureReason.ToString() : null));
        CreateMap<CreateProductItemRequest, ProductItem>()
            .ForMember(d => d.Id, o => o.Ignore())
            .ForMember(d => d.ProductId, o => o.Ignore())
            .ForMember(d => d.Product, o => o.Ignore());
        CreateMap<CreateProductImageRequest, ProductImage>()
            .ForMember(d => d.Id, o => o.Ignore())
            .ForMember(d => d.ProductId, o => o.Ignore())
            .ForMember(d => d.Product, o => o.Ignore());

        // Product detail
        CreateMap<Product, ProductDetailResponse>()
            .ForMember(d => d.StoreName, o => o.MapFrom(s => s.Store != null ? s.Store.Name : null))
            .ForMember(d => d.Categories, o => o.MapFrom(s => s.ProductCategories.Select(pc => pc.Category)))
            .ForMember(d => d.PrimaryElement, o => o.MapFrom(s =>
                s.Elements.Where(e => e.IsPrimary).Select(e => e.Element.ToString()).FirstOrDefault()))
            .ForMember(d => d.SecondaryElements, o => o.MapFrom(s =>
                s.Elements.Where(e => !e.IsPrimary).Select(e => e.Element.ToString()).ToList()))
            .ForMember(d => d.SizeClass, o => o.MapFrom(s => s.SizeClass != null ? s.SizeClass.ToString() : null))
            .ForMember(d => d.Vibes, o => o.MapFrom(s => s.Vibes.Select(v => v.VibeCode).ToList()))
            .ForMember(d => d.Styles, o => o.MapFrom(s => s.Styles.Select(st => st.StyleCode).ToList()));

        // Product list card
        CreateMap<Product, ProductListItemResponse>()
            .ForMember(d => d.MinPrice, o => o.MapFrom(s => s.Items.Any() ? (decimal?)s.Items.Min(i => i.Price) : null))
            .ForMember(d => d.PrimaryImageUrl, o => o.MapFrom(s => s.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).FirstOrDefault()))
            // Chỉ lộ model 3D khi thật sự xem được — cùng điều kiện với filter HasModel3D ở repository.
            .ForMember(d => d.Model3DUrl, o => o.MapFrom(s =>
                s.Model3D != null && s.Model3D.IsEnabled && s.Model3D.Status == Model3DStatus.Succeeded
                    ? s.Model3D.ModelUrl
                    : null))
            .ForMember(d => d.Model3DThumbnailUrl, o => o.MapFrom(s =>
                s.Model3D != null && s.Model3D.IsEnabled && s.Model3D.Status == Model3DStatus.Succeeded
                    ? s.Model3D.ThumbnailUrl
                    : null));
    }
}
