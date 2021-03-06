﻿using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SS.CMS.Abstractions;
using SS.CMS.Abstractions.Dto.Request;
using SS.CMS.Abstractions.Dto.Result;
using SS.CMS.Core.Office;
using SS.CMS.Extensions;

namespace SS.CMS.Web.Controllers.Admin.Settings.Users
{
    [Route("admin/settings/users")]
    public partial class UsersController : ControllerBase
    {
        private const string Route = "";
        private const string RouteImport = "actions/import";
        private const string RouteExport = "actions/export";
        private const string RouteCheck = "actions/check";
        private const string RouteLock = "actions/lock";
        private const string RouteUnLock = "actions/unLock";

        private readonly IAuthManager _authManager;
        private readonly IPathManager _pathManager;
        private readonly IDatabaseManager _databaseManager;
        private readonly IPluginManager _pluginManager;
        private readonly IUserRepository _userRepository;
        private readonly IUserGroupRepository _userGroupRepository;

        public UsersController(IAuthManager authManager, IPathManager pathManager, IDatabaseManager databaseManager, IPluginManager pluginManager, IUserRepository userRepository, IUserGroupRepository userGroupRepository)
        {
            _authManager = authManager;
            _pathManager = pathManager;
            _databaseManager = databaseManager;
            _pluginManager = pluginManager;
            _userRepository = userRepository;
            _userGroupRepository = userGroupRepository;
        }

        [HttpGet, Route(Route)]
        public async Task<ActionResult<GetResults>> Get([FromQuery]GetRequest request)
        {
            
            if (!await _authManager.IsAdminAuthenticatedAsync() ||
                !await _authManager.HasSystemPermissionsAsync(Constants.AppPermissions.SettingsUsers))
            {
                return Unauthorized();
            }

            var groups = await _userGroupRepository.GetUserGroupListAsync();

            var count = await _userRepository.GetCountAsync(request.State, request.GroupId, request.LastActivityDate, request.Keyword);
            var users = await _userRepository.GetUsersAsync(request.State, request.GroupId, request.LastActivityDate, request.Keyword, request.Order, request.Offset, request.Limit);

            return new GetResults
            {
                Users = users,
                Count = count,
                Groups = groups
            };
        }

        [HttpDelete, Route(Route)]
        public async Task<ActionResult<BoolResult>> Delete([FromBody] IdRequest request)
        {
            
            if (!await _authManager.IsAdminAuthenticatedAsync() ||
                !await _authManager.HasSystemPermissionsAsync(Constants.AppPermissions.SettingsUsers))
            {
                return Unauthorized();
            }

            var user = await _userRepository.DeleteAsync(request.Id);

            await _authManager.AddAdminLogAsync("删除用户", $"用户:{user.UserName}");

            return new BoolResult
            {
                Value = true
            };
        }

        [HttpPost, Route(RouteImport)]
        public async Task<ActionResult<ImportResult>> Import([FromForm] IFormFile file)
        {
            
            if (!await _authManager.IsAdminAuthenticatedAsync() ||
                !await _authManager.HasSystemPermissionsAsync(Constants.AppPermissions.SettingsUsers))
            {
                return Unauthorized();
            }

            if (file == null)
            {
                return this.Error("请选择有效的文件上传");
            }

            var fileName = Path.GetFileName(file.FileName);

            var sExt = PathUtils.GetExtension(fileName);
            if (!StringUtils.EqualsIgnoreCase(sExt, ".xlsx"))
            {
                return this.Error("导入文件为Excel格式，请选择有效的文件上传");
            }

            var filePath = _pathManager.GetTemporaryFilesPath(fileName);
            await _pathManager.UploadAsync(file, filePath);

            var errorMessage = string.Empty;
            var success = 0;
            var failure = 0;

            var sheet = ExcelUtils.GetDataTable(filePath);
            if (sheet != null)
            {
                for (var i = 1; i < sheet.Rows.Count; i++) //行
                {
                    if (i == 1) continue;

                    var row = sheet.Rows[i];

                    var userName = row[0].ToString().Trim();
                    var password = row[1].ToString().Trim();
                    var displayName = row[2].ToString().Trim();
                    var mobile = row[3].ToString().Trim();
                    var email = row[4].ToString().Trim();

                    if (!string.IsNullOrEmpty(userName) && !string.IsNullOrEmpty(password))
                    {
                        var (userId, message) = await _userRepository.InsertAsync(new User
                        {
                            UserName = userName,
                            DisplayName = displayName,
                            Mobile = mobile,
                            Email = email
                        }, password, string.Empty);
                        if (userId == 0)
                        {
                            failure++;
                            errorMessage = message;
                        }
                        else
                        {
                            success++;
                        }
                    }
                    else
                    {
                        failure++;
                    }
                }
            }

            return new ImportResult
            {
                Value = true,
                Success = success,
                Failure = failure,
                ErrorMessage = errorMessage
            };
        }

        [HttpGet, Route(RouteExport)]
        public async Task<ActionResult> Export()
        {
            
            if (!await _authManager.IsAdminAuthenticatedAsync() ||
                !await _authManager.HasSystemPermissionsAsync(Constants.AppPermissions.SettingsUsers))
            {
                return Unauthorized();
            }

            const string fileName = "users.csv";
            var filePath = _pathManager.GetTemporaryFilesPath(fileName);

            var excelObject = new ExcelObject(_databaseManager, _pluginManager);
            await excelObject.CreateExcelFileForUsersAsync(filePath, null);

            return this.Download(filePath);
        }

        [HttpPost, Route(RouteCheck)]
        public async Task<ActionResult<BoolResult>> Check([FromBody] IdRequest request)
        {
            
            if (!await _authManager.IsAdminAuthenticatedAsync() ||
                !await _authManager.HasSystemPermissionsAsync(Constants.AppPermissions.SettingsUsers))
            {
                return Unauthorized();
            }

            await _userRepository.CheckAsync(new List<int>
            {
                request.Id
            });

            await _authManager.AddAdminLogAsync("审核用户", $"用户Id:{request.Id}");

            return new BoolResult
            {
                Value = true
            };
        }

        [HttpPost, Route(RouteLock)]
        public async Task<ActionResult<BoolResult>> Lock([FromBody] IdRequest request)
        {
            
            if (!await _authManager.IsAdminAuthenticatedAsync() ||
                !await _authManager.HasSystemPermissionsAsync(Constants.AppPermissions.SettingsUsers))
            {
                return Unauthorized();
            }

            var user = await _userRepository.GetByUserIdAsync(request.Id);

            await _userRepository.LockAsync(new List<int>
            {
                request.Id
            });

            await _authManager.AddAdminLogAsync("锁定用户", $"用户:{user.UserName}");

            return new BoolResult
            {
                Value = true
            };
        }

        [HttpPost, Route(RouteUnLock)]
        public async Task<ActionResult<BoolResult>> UnLock([FromBody] IdRequest request)
        {
            
            if (!await _authManager.IsAdminAuthenticatedAsync() ||
                !await _authManager.HasSystemPermissionsAsync(Constants.AppPermissions.SettingsUsers))
            {
                return Unauthorized();
            }

            var user = await _userRepository.GetByUserIdAsync(request.Id);

            await _userRepository.UnLockAsync(new List<int>
            {
                request.Id
            });

            await _authManager.AddAdminLogAsync("解锁用户", $"用户:{user.UserName}");

            return new BoolResult
            {
                Value = true
            };
        }
    }
}
