using InkFlow.Modules.Library.Domain;

namespace InkFlow.Modules.Library.Application;

/// <summary>正典书籍仓储契约。实现负责聚合与实体的映射及章节增量持久化。</summary>
public interface ICanonicalBookRepository
{
    Task AddAsync(CanonicalBook book, CancellationToken cancellationToken = default);

    Task<CanonicalBook?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>全部书目(不含章节,供列表页使用)。</summary>
    Task<IReadOnlyList<CanonicalBook>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 按归一化的书名+作者查找已有正典书(Book Matcher v1 的同书识别依据);
    /// 未命中返回 null。
    /// </summary>
    Task<CanonicalBook?> FindByTitleAuthorAsync(
        string title, string author, CancellationToken cancellationToken = default);

    /// <summary>写回聚合的元数据与新增章节（已有章节不可变）。</summary>
    Task SaveAsync(CanonicalBook book, CancellationToken cancellationToken = default);
}
