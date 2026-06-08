using AutoMapper;
using FengDeskAI.Application.Features.Catalog.DTOs;
using FengDeskAI.Domain.Entities.Catalog;

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

        // Tag
        CreateMap<Tag, TagResponse>();
        CreateMap<Tag, TagRefResponse>();
        CreateMap<CreateTagRequest, Tag>().ForMember(d => d.Id, o => o.Ignore());

        // Product items & images
        CreateMap<ProductItem, ProductItemResponse>();
        CreateMap<ProductImage, ProductImageResponse>();
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
            .ForMember(d => d.Tags, o => o.MapFrom(s => s.ProductTags.Select(pt => pt.Tag)));

        // Product list card
        CreateMap<Product, ProductListItemResponse>()
            .ForMember(d => d.MinPrice, o => o.MapFrom(s => s.Items.Any() ? (decimal?)s.Items.Min(i => i.Price) : null))
            .ForMember(d => d.PrimaryImageUrl, o => o.MapFrom(s => s.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).FirstOrDefault()));
    }
}
