﻿using AutoMapper;
using SGRY.Api.Entities;
using SGRY.Api.Entities.Enums;
using SGRY.Api.Extensions;
using SGRY.Api.Extensions.AuthContext;
using SGRY.Api.Extensions.CustomException;
using SGRY.Api.Extensions.DataAccess;
using SGRY.Api.Models.Response;
using SGRY.Api.RequestPayload.Rbac.User;
using SGRY.Api.ViewModels.Rbac.DncUser;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Data.SqlClient;
using System.Linq;
using SGRY.Api.Utils;
using MySql.Data.MySqlClient;
/******************************************
* AUTHOR:          Rector
* CREATEDON:       2018-09-26
* OFFICIAL_SITE:    码友网(https://codedefault.com)--专注.NET/.NET Core
* 版权所有，请勿删除
******************************************/

namespace SGRY.Api.Controllers.Api.V1.Rbac
{
    /// <summary>
    /// 
    /// </summary>
    //[CustomAuthorize]
    [Route("api/v1/rbac/[controller]/[action]")]
    [ApiController]
    //[Authorize]
    public class UserController : ControllerBase
    {
        private readonly SGRYDbContext _dbContext;
        private readonly IMapper _mapper;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="dbContext"></param>
        /// <param name="mapper"></param>
        public UserController(SGRYDbContext dbContext, IMapper mapper)
        {
            _dbContext = dbContext;
            _mapper = mapper;
        }
        #region 修改密码
        /// <summary>
        /// 修改密码
        /// </summary>
        /// <param name="old">旧密码</param>
        /// <param name="pass">新密码</param>
        /// <returns></returns>
        [HttpPost]
        [ProducesResponseType(200)]
        public IActionResult ModPass(string old, string pass)
        {
            using (_dbContext)
            {

                var response = ResponseModelFactory.CreateInstance;
                var guid = AuthContextService.CurrentUser.Guid;
                var user = _dbContext.DncUser.FirstOrDefaultAsync(x => x.Guid == guid).Result;
                if (!user.Password.Equals(old))
                {
                    response.SetFailed("旧密码错误！");
                    return Ok(response);
                }
                user.Password = pass;
                _dbContext.SaveChanges();

                response.SetSuccess("密码修改成功");
                return Ok(response);
            }
        }
        #endregion

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        public IActionResult List(UserRequestPayload payload)
        {
            using (_dbContext)
            {
                var query = _dbContext.DncUser.AsQueryable();
                if (!string.IsNullOrEmpty(payload.Kw))
                {
                    query = query.Where(x => x.LoginName.Contains(payload.Kw.Trim()) || x.DisplayName.Contains(payload.Kw.Trim()));
                }
                if (payload.IsDeleted > CommonEnum.IsDeleted.All)
                {
                    query = query.Where(x => x.IsDeleted == payload.IsDeleted);
                }
                if (payload.Status > UserStatus.All)
                {
                    query = query.Where(x => x.Status == payload.Status);
                }

                if (payload.FirstSort != null)
                {
                    query = query.OrderBy(payload.FirstSort.Field, payload.FirstSort.Direct == "DESC");
                }
                var list = query.Paged(payload.CurrentPage, payload.PageSize).ToList();
                var totalCount = query.Count();
                var data = list.Select(_mapper.Map<DncUser, UserJsonModel>);
                var response = ResponseModelFactory.CreateResultInstance;
                response.SetData(data, totalCount);
                return Ok(response);
            }
        }

        /// <summary>
        /// 创建用户
        /// </summary>
        /// <param name="model">用户视图实体</param>
        /// <returns></returns>
        [HttpPost]
        [ProducesResponseType(200)]
        public IActionResult Create(UserCreateViewModel model)
        {
            var response = ResponseModelFactory.CreateInstance;
            if (model.LoginName.Trim().Length <= 0)
            {
                response.SetFailed("请输入登录名称");
                return Ok(response);
            }
            using (_dbContext)
            {
                if (_dbContext.DncUser.Count(x => x.LoginName == model.LoginName) > 0)
                {
                    response.SetFailed("登录名已存在");
                    return Ok(response);
                }
                var entity = _mapper.Map<UserCreateViewModel, DncUser>(model);
                entity.CreatedOn = DateTime.Now;
                entity.Guid = Guid.NewGuid();
                entity.Status = model.Status;
                _dbContext.DncUser.Add(entity);
                _dbContext.SaveChanges();

                response.SetSuccess();
                return Ok(response);
            }
        }

        /// <summary>
        /// 编辑用户
        /// </summary>
        /// <param name="guid">用户GUID</param>
        /// <returns></returns>
        [HttpGet("{guid}")]
        [ProducesResponseType(200)]
        public IActionResult Edit(Guid guid)
        {
            using (_dbContext)
            {
                var entity = _dbContext.DncUser.FirstOrDefault(x => x.Guid == guid);
                var response = ResponseModelFactory.CreateInstance;
                response.SetData(_mapper.Map<DncUser, UserEditViewModel>(entity));
                return Ok(response);
            }
        }

        /// <summary>
        /// 保存编辑后的用户信息
        /// </summary>
        /// <param name="model">用户视图实体</param>
        /// <returns></returns>
        [HttpPost]
        [ProducesResponseType(200)]
        public IActionResult Edit(UserEditViewModel model)
        {
            var response = ResponseModelFactory.CreateInstance;
            if (ConfigurationManager.AppSettings.IsTrialVersion)
            {
                response.SetIsTrial();
                return Ok(response);
            }
            using (_dbContext)
            {
                var entity = _dbContext.DncUser.FirstOrDefault(x => x.Guid == model.Guid);
                if (entity == null)
                {
                    response.SetFailed("用户不存在");
                    return Ok(response);
                }
                entity.DisplayName = model.DisplayName;
                entity.IsDeleted = model.IsDeleted;
                entity.IsLocked = model.IsLocked;
                entity.ModifiedByUserGuid = AuthContextService.CurrentUser.Guid;
                entity.ModifiedByUserName = AuthContextService.CurrentUser.DisplayName;
                entity.ModifiedOn = DateTime.Now;
                entity.Password = model.Password;
                entity.Status = model.Status;
                entity.UserType = model.UserType;
                entity.Description = model.Description;
                _dbContext.SaveChanges();
                response = ResponseModelFactory.CreateInstance;
                return Ok(response);
            }
        }

        /// <summary>
        /// 删除用户
        /// </summary>
        /// <param name="ids">用户GUID,多个以逗号分隔</param>
        /// <returns></returns>
        [HttpGet("{ids}")]
        [ProducesResponseType(200)]
        public IActionResult Delete(string ids)
        {
            var response = ResponseModelFactory.CreateInstance;
            if (ConfigurationManager.AppSettings.IsTrialVersion)
            {
                response.SetIsTrial();
                return Ok(response);
            }
            response = UpdateIsDelete(CommonEnum.IsDeleted.Yes, ids);
            return Ok(response);
        }

        /// <summary>
        /// 恢复用户
        /// </summary>
        /// <param name="ids">用户GUID,多个以逗号分隔</param>
        /// <returns></returns>
        [HttpGet("{ids}")]
        [ProducesResponseType(200)]
        public IActionResult Recover(string ids)
        {
            var response = UpdateIsDelete(CommonEnum.IsDeleted.No, ids);
            return Ok(response);
        }

        /// <summary>
        /// 批量操作
        /// </summary>
        /// <param name="command"></param>
        /// <param name="ids">用户ID,多个以逗号分隔</param>
        /// <returns></returns>
        [HttpGet]
        [ProducesResponseType(200)]
        public IActionResult Batch(string command, string ids)
        {
            var response = ResponseModelFactory.CreateInstance;
            switch (command)
            {
                case "delete":
                    if (ConfigurationManager.AppSettings.IsTrialVersion)
                    {
                        response.SetIsTrial();
                        return Ok(response);
                    }
                    response = UpdateIsDelete(CommonEnum.IsDeleted.Yes, ids);
                    break;
                case "recover":
                    response = UpdateIsDelete(CommonEnum.IsDeleted.No, ids);
                    break;
                case "forbidden":
                    if (ConfigurationManager.AppSettings.IsTrialVersion)
                    {
                        response.SetIsTrial();
                        return Ok(response);
                    }
                    response = UpdateStatus(UserStatus.Forbidden, ids);
                    break;
                case "normal":
                    response = UpdateStatus(UserStatus.Normal, ids);
                    break;
                default:
                    break;
            }
            return Ok(response);
        }

        #region 用户-角色
        /// <summary>
        /// 保存用户-角色的关系映射数据
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpPost("/api/v1/rbac/user/save_roles")]
        public IActionResult SaveRoles(SaveUserRolesViewModel model)
        {
            var response = ResponseModelFactory.CreateInstance;

            try
            {
                var roles = model.AssignedRoles.Select(x => new DncUserRoleMapping
                {
                    UserGuid = model.UserGuid,
                    CreatedOn = DateTime.Now,
                    RoleCode = x.Trim()
                }).ToList();
                _dbContext.Database.ExecuteSqlCommand("DELETE FROM DncUserRoleMapping WHERE UserGuid={0}", model.UserGuid.ToString("D"));
                var success = true;
                if (roles.Count > 0)
                {
                    _dbContext.DncUserRoleMapping.AddRange(roles);
                    success = _dbContext.SaveChanges() > 0;
                }

                if (success)
                {
                    response.SetSuccess();
                }
                else
                {
                    response.SetFailed("保存用户角色数据失败");
                }
                return Ok(response);
            }
            catch (Exception M)
            {
                response.SetError(M.Message);
                return Ok(response);
            }
            
        }
        #endregion

        #region 私有方法
        /// <summary>
        /// 删除用户
        /// </summary>
        /// <param name="isDeleted"></param>
        /// <param name="ids">用户ID字符串,多个以逗号隔开</param>
        /// <returns></returns>
        private ResponseModel UpdateIsDelete(CommonEnum.IsDeleted isDeleted, string ids)
        {
            using (_dbContext)
            {
                if (ToolService.DbType.Equals("mysql"))
                {
                    var parameters = ids.Split(",").Select((id, index) => new MySqlParameter(string.Format("@p{0}", index), id)).ToList();
                    var parameterNames = string.Join(", ", parameters.Select(p => p.ParameterName));
                    var sql = string.Format("UPDATE DncUser SET IsDeleted=@IsDeleted WHERE Guid IN ({0})", parameterNames);
                    parameters.Add(new MySqlParameter("@IsDeleted", (int)isDeleted));
                    _dbContext.Database.ExecuteSqlCommand(sql, parameters);
                    var response = ResponseModelFactory.CreateInstance;
                    return response;
                }
                else
                {
                    var parameters = ids.Split(",").Select((id, index) => new SqlParameter(string.Format("@p{0}", index), id)).ToList();
                    var parameterNames = string.Join(", ", parameters.Select(p => p.ParameterName));
                    var sql = string.Format("UPDATE DncUser SET IsDeleted=@IsDeleted WHERE Guid IN ({0})", parameterNames);
                    parameters.Add(new SqlParameter("@IsDeleted", (int)isDeleted));
                    _dbContext.Database.ExecuteSqlCommand(sql, parameters);
                    var response = ResponseModelFactory.CreateInstance;
                    return response;
                }
                    
            }
        }

        /// <summary>
        /// 删除用户
        /// </summary>
        /// <param name="status">用户状态</param>
        /// <param name="ids">用户ID字符串,多个以逗号隔开</param>
        /// <returns></returns>
        private ResponseModel UpdateStatus(UserStatus status, string ids)
        {
            using (_dbContext)
            {
                if (ToolService.DbType.Equals("mysql"))
                {
                    var parameters = ids.Split(",").Select((id, index) => new MySqlParameter(string.Format("@p{0}", index), id)).ToList();
                    var parameterNames = string.Join(", ", parameters.Select(p => p.ParameterName));
                    var sql = string.Format("UPDATE DncUser SET Status=@Status WHERE Guid IN ({0})", parameterNames);
                    parameters.Add(new MySqlParameter("@Status", (int)status));
                    _dbContext.Database.ExecuteSqlCommand(sql, parameters);
                    var response = ResponseModelFactory.CreateInstance;
                    return response;
                }
                else
                {
                    var parameters = ids.Split(",").Select((id, index) => new SqlParameter(string.Format("@p{0}", index), id)).ToList();
                    var parameterNames = string.Join(", ", parameters.Select(p => p.ParameterName));
                    var sql = string.Format("UPDATE DncUser SET Status=@Status WHERE Guid IN ({0})", parameterNames);
                    parameters.Add(new SqlParameter("@Status", (int)status));
                    _dbContext.Database.ExecuteSqlCommand(sql, parameters);
                    var response = ResponseModelFactory.CreateInstance;
                    return response;
                }
                    
            }
        }
        #endregion
    }
}