/*
 * Copyright (c) Contributors, http://whitecore-sim.org/, http://aurora-sim.org
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the WhiteCore-Sim Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System.Collections.Generic;
using OpenMetaverse;
using WhiteCore.Framework.DatabaseInterfaces;
using WhiteCore.Framework.Servers.HttpServer.Implementation;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Services.ClassHelpers.Profile;

namespace WhiteCore.Modules.Web
{
	public class UserPartnershipPage : IWebInterfacePage
	{
		public string [] FilePath {
			get {
				return new [] {
					"html/user/partnership.html"
				};
			}
		}

		public bool RequiresAuthentication {
			get { return true; }
		}

		public bool RequiresAdminAuthentication {
			get { return false; }
		}
        
		public Dictionary<string, object> Fill(WebInterface webInterface, string filename, OSHttpRequest httpRequest,
			OSHttpResponse httpResponse, Dictionary<string, object> requestParameters,
			ITranslator translator, out string response)
		{
			response = null;
        	
			var vars = new Dictionary<string, object>();

            // This page should show if a user already has a partner and show the ability to cancel the Partnership (with a payment defined in Economy.ini)
            // 
            // If the user doesn't have a partner, allow the user to send an Partnership invite to a person (internally send a message inworld to the person)
            //
            UserAccount userAcct = Authenticator.GetAuthentication(httpRequest);
            if (userAcct == null) {
                response = webInterface.UserMsg("!Unable to authenticate user details", true);
                return null;
            }

            // who we are dealing with...
            vars.Add("UserName", userAcct.Name);

            var profileService = Framework.Utilities.DataManager.RequestPlugin<IProfileConnector>();
            if (profileService != null) {
                IUserProfileInfo userprofile = profileService.GetUserProfile(userAcct.PrincipalID);
                if (userprofile != null) {
                    if (userprofile.Partner != UUID.Zero) {
                        UserAccount partnerAcct = webInterface.Registry.RequestModuleInterface<IUserAccountService>().GetUserAccount(null, userprofile.Partner);
                        vars.Add("UserPartner", partnerAcct.Name);
                    } else {
                        vars.Add("UserPartner", "No partner");
                    }
                } else {
                    vars.Add("UserPartner", "No partner");
                }
            } else {
                response = webInterface.UserMsg("!Unable to connect to user profile service", true);
                return null;
            }

            // partner change
            if (requestParameters.ContainsKey("Submit")) {
                string newpartner = requestParameters["partnernew"].ToString();
                string partnerporposal = requestParameters["partnerporposal"].ToString();
                UserAccount partnerAcct = new UserAccount();

                if (string.IsNullOrEmpty(newpartner)) {
                    response = webInterface.UserMsg("!Partner's name not supplied", false);
                } else {
                    IUserAccountService userService = webInterface.Registry.RequestModuleInterface<IUserAccountService>();
                    if (userService != null) {
                        partnerAcct = userService.GetUserAccount(null, newpartner);
                        if (partnerAcct.Valid) {
                            if (userAcct.PrincipalID == partnerAcct.PrincipalID) {
                                response = webInterface.UserMsg("!Unable to set yourself as your partner", true);
                            } else {
                                // send the proposal
                                IGridWideMessageModule messageModule = webInterface.Registry.RequestModuleInterface<IGridWideMessageModule>();
                                if (messageModule != null) {
                                    messageModule.MessageUser(partnerAcct.PrincipalID, partnerporposal);
                                    response = webInterface.UserMsg("The proposal has been sent to " + newpartner, true);
                                } else {
                                    response = webInterface.UserMsg("!Messaging is not currently available. Please retry later", true);
                                }
                            }
                            // This would be used to set immediatly??
                            /*
                            IUserProfileInfo partnerProfile = profileService.GetUserProfile(partnerAcct.PrincipalID);
                            if (partnerProfile == null) {
                                response = webInterface.UserMsg("!Partner does not have a profile yet!", true);
                            } else {
                                userprofile.Partner = partnerProfile.PrincipalID;
                                partnerProfile.Partner = userprofile.PrincipalID;

                                profileService.UpdateUserProfile(userprofile);
                                profileService.UpdateUserProfile(partnerProfile);
                                response = webInterface.UserMsg("Your partner has been updated", true);
                           }
                            */

                        } else {
                            response = webInterface.UserMsg("!Unable to retrieve partner's account details", true);
                        }
                    } else {
                        response = webInterface.UserMsg("!The accounting service is not currently available. Please retry later", true);
                    }
                }
                // we should have a go/no go response here
                return null;
            }

            // Page variables
            vars.Add("PartnersName", translator.GetTranslatedString("PartnersName"));
            vars.Add("PartnerProposalText", translator.GetTranslatedString("PartnerProposalText"));
            vars.Add("CancelText", translator.GetTranslatedString("Cancel"));
            vars.Add("SendProposalText", translator.GetTranslatedString("SendProposalText"));

            return vars;
		}
        
		public bool AttemptFindPage(string filename, ref OSHttpResponse httpResponse, out string text)
		{
			text = "";
			return false;
		}
	}
}
