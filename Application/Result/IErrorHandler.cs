﻿using Application.UseCases.Repository.UseCases.CRUD;

namespace Application.Result
{
    /// <summary>
    /// Defines methods to load error mappings and create failed operations.
    /// </summary>
    public interface IErrorHandler
    {
        /// <summary>
        /// Load error codes and messages from a configuration file.
        /// </summary>
        void LoadErrorMappings(string filePath);

        /// <summary>
        /// Create a failed operation with an exception and a custom message.
        /// </summary>
        Operation<T> Fail<T>(Exception? ex, string errorMessage, IErrorLogCreate errorLogCreate);

        /// <summary>
        /// Create a failed operation from an exception using default message.
        /// </summary>
        Operation<T> Fail<T>(Exception? ex, IErrorLogCreate errorLogCreate);

        /// <summary>
        /// Create a business error operation with a message.
        /// </summary>
        Operation<T> Business<T>(string errorMessage);

        /// <summary>
        /// Check if any error mappings are loaded.
        /// </summary>
        bool Any();
    }
}
