﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SS.CMS.Abstractions;
using SS.CMS.Abstractions.Dto.Request;
using SS.CMS.Core;

namespace SS.CMS.Web.Controllers.Admin.Cms.Contents
{
    public partial class ContentsCheckController
    {
        [HttpPost, Route(RouteList)]
        public async Task<ActionResult<ListResult>> List([FromBody] ListRequest request)
        {
            
            if (!await _authManager.IsAdminAuthenticatedAsync() ||
                !await _authManager.HasSitePermissionsAsync(request.SiteId,
                    Constants.SitePermissions.ContentsCheck))
            {
                return Unauthorized();
            }

            var site = await _siteRepository.GetAsync(request.SiteId);
            if (site == null) return NotFound();

            var channel = await _channelRepository.GetAsync(request.SiteId);

            var columnsManager = new ColumnsManager(_databaseManager, _pluginManager);
            var columns = await columnsManager.GetContentListColumnsAsync(site, channel, ColumnsManager.PageType.CheckContents);

            var pageContents = new List<Content>();
            var (total, pageSummaries) = await _contentRepository.CheckSearch(site, request.Page, request.ChannelId, request.StartDate, request.EndDate, request.Items, request.IsCheckedLevels, request.CheckedLevels, request.IsTop, request.IsRecommend, request.IsHot, request.IsColor, request.GroupNames, request.TagNames);

            if (total > 0)
            {
                var offset = site.PageSize * (request.Page - 1);

                var sequence = offset + 1;
                foreach (var summary in pageSummaries)
                {
                    var content = await _contentRepository.GetAsync(site, summary.ChannelId, summary.Id);
                    if (content == null) continue;

                    var pageContent =
                        await columnsManager.CalculateContentListAsync(sequence++, site, request.SiteId, content, columns, null);

                    pageContents.Add(pageContent);
                }
            }

            return new ListResult
            {
                PageContents = pageContents,
                Total = total,
                PageSize = site.PageSize
            };
        }

        public class ListRequest : SiteRequest
        {
            public int? ChannelId { get; set; }
            public DateTime? StartDate { get; set; }
            public DateTime? EndDate { get; set; }
            public IEnumerable<KeyValuePair<string, string>> Items { get; set; }
            public int Page { get; set; }
            public bool IsCheckedLevels { get; set; }
            public List<int> CheckedLevels { get; set; }
            public bool IsTop { get; set; }
            public bool IsRecommend { get; set; }
            public bool IsHot { get; set; }
            public bool IsColor { get; set; }
            public List<string> GroupNames { get; set; }
            public List<string> TagNames { get; set; }
        }

        public class ListResult
        {
            public List<Content> PageContents { get; set; }
            public int Total { get; set; }
            public int PageSize { get; set; }
        }
    }
}
