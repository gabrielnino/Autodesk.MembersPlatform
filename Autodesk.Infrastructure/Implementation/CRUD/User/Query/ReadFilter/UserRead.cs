﻿using Application.Common.Pagination;
using Application.Result;
using Application.UseCases.Repository.UseCases.CRUD;
using Autodesk.Application.UseCases.CRUD.User.Query;
using Autodesk.Domain;
using Infrastructure.Repositories.Abstract.CRUD.Query.Read;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using Persistence.Context.Implementation;
using Persistence.Context.Interface;
using System;
using System.Linq.Expressions;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Reads users with filters, paging, and caching.
/// </summary>
public class UserRead(IUnitOfWork unitOfWork, IErrorHandler errorHandler, IMemoryCache cache, IErrorLogCreate errorLogCreate) : ReadRepository<User>(unitOfWork, q => q.OrderBy(u => u.Name!).ThenBy(u => u.Id)), IUserRead
{
    private readonly IErrorHandler errorHandler = errorHandler;
    private readonly IMemoryCache cache = cache;
    private readonly Func<User, (string Primary, string Secondary)> cursorSelector = u => (u.Name!, u.Id);
    private static CancellationTokenSource _userCacheTokenSource = new();


    /// <summary>
    /// Fetch a page of users filtered by id or name, with cursor-based paging and caching.
    /// </summary>
    /// <param name="id">Optional user ID filter.</param>
    /// <param name="name">Optional user name filter.</param>
    /// <param name="cursor">Cursor for the next page.</param>
    /// <param name="pageSize">Number of items per page.</param>
    /// <returns>Operation result with paged users or error.</returns>
    public async Task<Operation<PagedResult<User>>> GetUsersPageAsync(
        string? id,
        string? name,
        string? cursor,
        int pageSize)
    {
        try
        {
            var cacheKey = $"users:{id}:{name}:{cursor}:{pageSize}";
            if (cache.TryGetValue(cacheKey, out PagedResult<User> cached))
            {
                return Operation<PagedResult<User>>.Success(cached);
            }
            var result = await GetPageAsync(BuildFilter(id, name), cursor, pageSize);
            var pagedResult = result.Data;


            var cacheOptions = new MemoryCacheEntryOptions()
                .AddExpirationToken(new CancellationChangeToken(_userCacheTokenSource.Token))
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(5));

            cache.Set(cacheKey, pagedResult, cacheOptions);

            return Operation<PagedResult<User>>.Success(pagedResult);
        }
        catch (Exception ex)
        {
            return errorHandler.Fail<PagedResult<User>>(ex, errorLogCreate);
        }
    }

    /// <summary>
    /// Choose filter by id or name, or none.
    /// </summary>
    private static Expression<Func<User, bool>> BuildFilter(string? id, string? name)
    {
        if (ShouldFilterById(id))
        {
            return BuildIdFilter(id!);
        }
           
        if (ShouldFilterByName(name))
        {
            return BuildNameFilter(name!);
        }
            
        return ReturnDefaultFilter();
    }

    /// <summary>
    /// Check if id is provided.
    /// </summary>
    private static bool ShouldFilterById(string? id) => !string.IsNullOrWhiteSpace(id);

    /// <summary>
    /// Check if name is provided.
    /// </summary>
    private static bool ShouldFilterByName(string? name) => !string.IsNullOrWhiteSpace(name);

    /// <summary>
    /// Filter users by id.
    /// </summary>
    private static Expression<Func<User, bool>> BuildIdFilter(string id) => u => u.Id == id;

    /// <summary>
    /// Filter users by name pattern.
    /// </summary>
    private static Expression<Func<User, bool>> BuildNameFilter(string name) => u => EF.Functions.Like(u.Name!, $"%{name}%");

    /// <summary>
    /// No filter: return all users.
    /// </summary>
    private static Expression<Func<User, bool>> ReturnDefaultFilter() => u => true;

    protected override IQueryable<User> ApplyCursorFilter(IQueryable<User> query, string cursor)
    {
        var parts = Uri.UnescapeDataString(cursor).Split('|', 2);
        var name = parts[0];
        var lastS = parts.Length > 1 ? parts[1] : string.Empty;

        return query.Where
        (
            u => DataContext.StringCompareOrdinal(u.Name!, name) > 0 || (u.Name == name && DataContext.StringCompareOrdinal(u.Id, lastS) > 0)
        );
    }

    protected override string? BuildNextCursor(List<User> items, int size)
    {
        if (items.Count <= size) return null;
        var extra = items[size];
        var (p, s) = cursorSelector(extra);
        return Uri.EscapeDataString($"{p}|{s}");
    }

    public void InvalidateAllUserCache()
    {
        // Disparamos el token actual, invalidando todo lo cacheado con él
        _userCacheTokenSource.Cancel();
        // Creamos un nuevo CancellationTokenSource para futuras operaciones
        _userCacheTokenSource = new CancellationTokenSource();
    }

    public List<User> GetStreamUsers(CancellationToken cancellationToken = default)
    {
        var result = GetAllMembers(cancellationToken).Result;
        return [.. result.Data.Items];
    }
}
