import Vue from 'vue';
import Vuex from 'vuex';
Vue.use(Vuex);
import createPersistedState from 'vuex-persistedstate';
import ExecAPI from 'appRoot/api/api';
import bcrypt from 'appRoot/scripts/bcrypt.min.js';

export default function CreateStore()
{
	return new Vuex.Store({
		strict: process.env.NODE_ENV !== 'production',
		plugins: [createPersistedState({
			storage: window.localStorage,
			setState: SetState,
			getState: GetState
		})],
		state:
		{
			sid: "",
			isAdmin: false,
			projectConfig: {},
			eventBodyBelow: true
			/*
			 Example projectConfig layout:
			 projectConfig: {
				project1: {
					collapsedFolders: {
						0: false
						1: true,
						5: true
					},
					folderBrowserSize: 150,
					eventBrowserSize: 400,
					topBrowsersSize: 400
				}
			 }
			 */
		},
		mutations: // mutations must not be async
		{
			SessionLost(state)
			{
				state.sid = "";
				state.isAdmin = false;
			},
			SessionAuthenticated(state, { sid, isAdmin })
			{
				state.sid = sid;
				state.isAdmin = isAdmin;
			},
			SetSid(state, sid)
			{
				state.sid = sid;
			},
			SetFolderCollapsedState(state, { projectName, folderId, collapsed })
			{
				let pcfg = GetProjectConfig(state, projectName);
				if (!pcfg.collapsedFolders)
					Vue.set(pcfg, "collapsedFolders", {});
				Vue.set(pcfg.collapsedFolders, folderId, !!collapsed);
			},
			SetFolderBrowserSize(state, { projectName, size })
			{
				let pcfg = GetProjectConfig(state, projectName);
				Vue.set(pcfg, "folderBrowserSize", size);
			},
			SetEventBrowserSize(state, { projectName, size })
			{
				let pcfg = GetProjectConfig(state, projectName);
				Vue.set(pcfg, "eventBrowserSize", size);
			},
			SetTopBrowsersSize(state, { projectName, size })
			{
				let pcfg = GetProjectConfig(state, projectName);
				Vue.set(pcfg, "topBrowsersSize", size);
			},
			SetEventBodyBelow(state, below)
			{
				state.eventBodyBelow = !!below;
			}
		},
		actions: // actions can be async
		{
			/**
			 * Performs the authentication protocol. Returns a promise that resolves if login is successful, otherwise rejects with a string containing an error message to show.
			 * @param {Object} param0 Action context.
			 * @param {Object} param1 Arguments to this action.
			 * @returns {Promise} A promise that resolves if login is successful, otherwise rejects with a string containing an error message to show.
			 */
			Login({ commit, dispatch, state, rootState, rootGetters }, { user, pass })
			{
				return ExecAPI("Auth/Login", { user })
					.then(data =>
					{
						if (data.success)
						{
							if (data.message)
								return Promise.reject(data.message);

							if (!data.authenticated)
							{
								// Expected result.

								if (data.sid && rootState.sid !== data.sid)
									commit("SetSid", data.sid);

								if (data.challenge)
								{
									let args = { sid: data.sid, user };
									try
									{
										// Use BCrypt on the password, using the salt provided by the server.
										var bCryptResult = bcrypt.hashSync(pass, data.salt);
										// Compute SHA512 so we have the desired output size for later XORing
										var bCryptResultHex = Util.bytesToHex(Util.stringToUtf8ByteArray(bCryptResult));
										var onceHashedPw = Util.ComputeSHA512Hex(bCryptResultHex);
										// We prove our identity by transmitting onceHashedPw to the server.
										// However we won't do that in plain text.
										// Hash one more time; PasswordHash is the value remembered by the server
										var PasswordHash = Util.ComputeSHA512Hex(onceHashedPw);
										var challengeHashed = Util.ComputeSHA512Hex(PasswordHash + data.challenge);
										args.token = Util.XORHexStrings(challengeHashed, onceHashedPw);
									}
									catch (ex)
									{
										return Promise.reject("An error occurred while creating the authentication token: \n" + ex.stack);
									}
									return ExecAPI("Auth/Login", args)
										.then(data =>
										{
											if (data.success)
											{
												if (data.message)
													return Promise.reject(data.message);

												if (data.authenticated)
												{
													commit("SessionAuthenticated", { sid: data.sid, isAdmin: data.isAdmin });
													return data;
												}
												else
													return Promise.reject("Login protocol failed without a message.");
											}
											else
												return Promise.reject(data.error);
										});
								}
								else
									return Promise.reject("Login protocol failed. Expected challenge.");
							}
							else
							{
								return Promise.reject("Login protocol failed. Server indicated that session was authenticated before providing credentials.");
							}
						}
						else
						{
							return Promise.reject(data.error);
						}
					})
					.catch(err =>
					{
						if (typeof err === "string")
							return Promise.reject(err);
						else if (err && typeof err.message === "string")
							return Promise.reject(err.message);
						else
							return Promise.reject("An unhandled error occurred.");
					});
			},

			/**
			 * Logs out of the current session. Returns a promise that resolves if logout is successful, otherwise rejects with an error object
			 * @param {Object} param0 Action context.
			 * @param {Object} param1 Arguments to this action.
			 * @returns {Promise} Returns a promise that resolves if logout is successful, otherwise rejects with an error object.
			 */
			Logout({ commit, dispatch, state, rootState, rootGetters })
			{
				return ExecAPI("Auth/Logout")
					.then(data =>
					{
						commit("SessionLost");
						return data;
					});
			}
		},
		getters:
		{
			sid(state)
			{
				return state.sid;
			},
			isFolderCollapsed(state)
			{
				return (projectName, folderId) =>
				{
					let pcfg = GetProjectConfig(state, projectName, true);
					if (pcfg && pcfg.collapsedFolders)
						return !!pcfg.collapsedFolders[folderId];
					return false;
				};
			},
			folderBrowserSize(state)
			{
				return (projectName) =>
				{
					let pcfg = GetProjectConfig(state, projectName, true);
					if (pcfg && pcfg.folderBrowserSize)
						return pcfg.folderBrowserSize;
					return 175;
				};
			},
			eventBrowserSize(state)
			{
				return (projectName) =>
				{
					let pcfg = GetProjectConfig(state, projectName, true);
					if (pcfg && pcfg.eventBrowserSize)
						return pcfg.eventBrowserSize;
					return 400;
				};
			},
			topBrowsersSize(state)
			{
				return (projectName) =>
				{
					let pcfg = GetProjectConfig(state, projectName, true);
					if (pcfg && pcfg.topBrowsersSize)
						return pcfg.topBrowsersSize;
					return 400;
				};
			}
		}
	});
}
function SetState(storeName, state, storage)
{
	//console.log("SetState", storeName, "state", JSON.parse(JSON.stringify(state)));
	storeName = "errtrk";
	for (let key in state)
	{
		if (state.hasOwnProperty(key))
		{
			let value = state[key];
			storage.setItem(storeName + "_" + key, JSON.stringify(value));
		}
	}
}
function GetState(storeName, storage, value)
{
	//console.log("GetState", storeName);
	storeName = "errtrk";
	try
	{
		let newObj = new Object();
		for (let i = 0; i < storage.length; i++)
		{
			let key = storage.key(i);
			if (key.substr(0, storeName.length + 1) === storeName + "_")
			{
				value = storage.getItem(key);
				if (typeof value !== 'undefined')
					newObj[key.substr(storeName.length + 1)] = JSON.parse(value);
			}
		}
		return newObj;
	}
	catch (ex)
	{
		console.error(ex); // toaster is not always available here.
	}

	return undefined;
}
/**
 * Gets the projectConfig object for the specified project, optionally creating it if necessary. Returns null if automatic creation is necessary but disabled.
 * @param {any} state
 * @param {any} projectName
 * @param {any} getOnly Pass true to disable automatic creation (for use in store getters)
 */
function GetProjectConfig(state, projectName, getOnly)
{
	if (!state.projectConfig)
	{
		if (getOnly)
			return null;
		Vue.set(state, "projectConfig", {});
	}
	let pcfg = state.projectConfig[projectName];
	if (!pcfg)
	{
		if (getOnly)
			return null;
		Vue.set(state.projectConfig, projectName, {});
		pcfg = state.projectConfig[projectName];
	}
	return pcfg;
}