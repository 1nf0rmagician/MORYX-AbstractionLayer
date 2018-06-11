﻿using System.Collections.Generic;
using Marvin.Model;

namespace Marvin.Products.Model
{
    /// <summary>
    /// The public API of the ArticleEntity repository.
    /// </summary>
    public interface IArticleEntityRepository : IRepository<ArticleEntity>
    {
        /// <summary>
        /// Get all ArticleEntitys where State equals given value
        /// </summary>
        /// <param name="state">Value the entities have to match</param>
        /// <returns>Collection of all matching ArticleEntitys</returns>
        ICollection<ArticleEntity> GetAllByState(long state);
    }
}