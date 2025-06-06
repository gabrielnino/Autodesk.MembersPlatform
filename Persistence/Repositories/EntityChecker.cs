﻿using Domain.Interfaces.Entity;
using Microsoft.EntityFrameworkCore;
using Persistence.Context.Interface;

namespace Persistence.Repositories
{
    /// <summary>
    /// Provides methods to check if an entity exists in the database.
    /// </summary>
    public abstract class EntityChecker<T>(IUnitOfWork unitOfWork) : Read<T>(unitOfWork) where T : class, IEntity
    {
        /// <summary>
        /// Finds an entity by its ID. Returns the entity or null.
        /// </summary>
        protected async Task<T?> HasEntity(string id)
        {
            var results = await ReadFilter(e => e.Id == id);
            var entity = results?.FirstOrDefault();
            return entity;
        }

        /// <summary>
        /// Validates the ID string and then checks for the entity.
        /// </summary>
        protected async Task<T?> HasId(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            if (!GuidValidator.HasGuid(id))
            {
                return null;
            }

            return await HasEntity(id);
        }

        /// <summary>
        /// Utility to verify if a string is a valid GUID.
        /// </summary>
        private class GuidValidator
        {
            public static bool HasGuid(string id) => Guid.TryParse(id, out _);
        }
    }
}
