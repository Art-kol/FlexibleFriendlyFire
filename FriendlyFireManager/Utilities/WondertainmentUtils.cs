using System;
using System.Collections.Generic;
using PlayerRoles;
using UnityEngine;

namespace FriendlyFireManager.Utilities {
    public class WondertainmentUtils {
        /// <summary>
        ///     Parses a list of IDs separated by dots (e.g., "1.4.8.15")
        /// </summary>
        /// <param name="input">The input string containing IDs separated by dots</param>
        /// <param name="Ids">Output list of parsed IDs</param>
        /// <returns>True if parsing succeeded, false otherwise</returns>
        public static bool TryParseRoleTypeIDList(string input, out List<long> Ids) {
            Ids = [];

            try {
                if (string.IsNullOrEmpty(input)) {
                    return false;
                }

                var splitIds = input.Split(['.'], StringSplitOptions.RemoveEmptyEntries);

                foreach (var idStr in splitIds) {
                    if (!int.TryParse(idStr, out var Id)) {
                        return false;
                    }

                    Ids.Add(Id);
                }

                return true;
            }
            catch (Exception ex) {
                Debug.LogException(ex);
                return false;
            }
        }

        public static bool TryParseRoleTypeIDsList(string input, out List<RoleTypeId> roleIds) {
            roleIds = [];

            if (!TryParseRoleTypeIDList(input, out var ids)) {
                return false;
            }

            foreach (var id in ids) {
                if (!Enum.IsDefined(typeof(RoleTypeId), (RoleTypeId)id)) {
                    return false;
                }

                roleIds.Add((RoleTypeId)id);
            }

            return true;
        }
    }
}