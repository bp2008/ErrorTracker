import Vue from 'vue';
import VueRouter from 'vue-router';
import ExecAPI from 'appRoot/api/api';

import Login from 'appRoot/vues/Login.vue';
import ClientLayout from 'appRoot/vues/client/ClientLayout.vue';
import ClientHome from 'appRoot/vues/client/ClientHome.vue';
import ClientFiltersHome from 'appRoot/vues/client/filters/ClientFiltersHome.vue';
import ChangePassword from 'appRoot/vues/client/ChangePassword.vue';
import AdvancedSearchHome from 'appRoot/vues/client/search/AdvancedSearchHome.vue';

import AdminLayout from 'appRoot/vues/admin/AdminLayout.vue';
import AdminHome from 'appRoot/vues/admin/AdminHome.vue';
import AdminSettings from 'appRoot/vues/admin/AdminSettings.vue';
import AdminUsers from 'appRoot/vues/admin/AdminUsers.vue';
import AdminProjects from 'appRoot/vues/admin/AdminProjects.vue';

Vue.use(VueRouter);

function safeJsonParse(str)
{
	try
	{
		return JSON.parse(str);
	}
	catch (ex)
	{
		return null;
	}
}

export default function CreateRouter(store, basePath)
{
	// Detect if a custom basePath string is being used by the browser right now.  If not, replace it with the default "/" basePath.
	let addBasePathSpecialRoute = false;
	let basePathLower = basePath.toLowerCase();
	let pathLower = location.pathname.toLowerCase();
	if (!pathLower.startsWith(basePathLower))
	{
		// Our base path is not found at the start of the URL
		// But maybe we have everything except the ending forward slash?
		// E.g. "http://127.0.0.1/basePath"
		if (basePathLower.charAt(basePathLower.length - 1) === '/' && pathLower === basePathLower.substr(0, basePathLower.length - 1))
			addBasePathSpecialRoute = true;
		else
			basePath = "/"; // Nope. Base path is not being used.
	}
	const router = new VueRouter({
		mode: 'history',
		routes: [
			{
				path: basePath + '', redirect: basePath + 'login'
			},
			{
				path: basePath + 'login', component: Login, name: 'login'
			},
			{
				path: basePath + 'client', component: ClientLayout,
				children: [
					{ path: '', redirect: 'home' },
					{
						path: 'home', component: ClientHome, name: 'clientHome', meta: {
							title(r)
							{
								return (r.query && r.query.p ? (r.query.p + " - ") : "") + "Home"
							}
						},
						props: (route) => ({
							projectName: route.query.p ? route.query.p.toString() : "",
							selectedFolderId: route.query.f ? parseInt(route.query.f) : 0,
							openedEventId: route.query.e ? route.query.e : null,
							selectedEventIds: route.query.se ? route.query.se : "",
							searchArgs: {
								query: route.query.q,
								matchAny: route.query.matchAny ? route.query.matchAny === "1" : false,
								conditions: route.query.scon ? safeJsonParse(route.query.scon) : null
							}
						})
					},
					{
						path: 'filters/:filterId*', component: ClientFiltersHome, name: 'clientFilters'
						, meta: {
							title(r)
							{
								return (r.query && r.query.p ? (r.query.p + " - ") : "") + "Filters"
							}
						}
						, props: (route) => ({
							projectName: route.query.p ? route.query.p.toString() : "",
							filterId: route.params.filterId ? route.params.filterId.toString() : ""
						})
					},
					{
						path: 'advancedSearch', component: AdvancedSearchHome, name: 'advancedSearch'
						, meta: {
							title(r)
							{
								return (r.query && r.query.p ? (r.query.p + " - ") : "") + "Advanced Search"
							}
						}
						, props: (route) => ({
							projectName: route.query.p ? route.query.p.toString() : "",
							selectedFolderId: route.query.f ? parseInt(route.query.f) : 0
						})
					},
					{
						path: 'changePassword', component: ChangePassword, name: 'changePassword', meta: { title: "Change Password" }
					}
				]
			},
			{
				path: basePath + 'admin', component: AdminLayout,
				children: [
					{ path: '', redirect: 'home' },
					{ path: 'home', component: AdminHome, name: 'adminHome', meta: { title: "Home" } },
					{ path: 'settings', component: AdminSettings, name: 'adminSettings', meta: { title: "Settings" } },
					{ path: 'users', component: AdminUsers, name: 'adminUsers', meta: { title: "Users" } },
					{ path: 'projects', component: AdminProjects, name: 'adminProjects', meta: { title: "Projects" } }
				]
			}
		],
		$store: store
	});
	if (addBasePathSpecialRoute)
		router.addRoutes([{ path: basePath.substr(0, basePath.length - 1), redirect: basePath + 'login' }]);
	router.onError(function (error)
	{
		console.error("Error while routing", error);
		toaster.error('Routing Error', error);
	});

	//router.beforeEach((to, from, next) =>
	//{
	//	next();
	//});

	router.afterEach((to, from) =>
	{
		Vue.nextTick(() =>
		{
			// Set the page title. This is being done on next tick in order to ensure that the title is applied to the correct history item in the browser's html5 history stack.
			let titleArr = [];
			let routeSetsOwnTitle = false;
			for (let i = to.matched.length - 1; i >= 0; i--)
			{
				let r = to.matched[i];
				if (typeof r.meta !== 'undefined' && r.meta)
				{
					if (!routeSetsOwnTitle)
						routeSetsOwnTitle = typeof r.meta.setsOwnTitle !== 'undefined' && r.meta.setsOwnTitle;
					if (typeof r.meta.title !== 'undefined')
					{
						if (typeof r.meta.title === 'function')
							titleArr.push(r.meta.title(to));
						else
							titleArr.push(r.meta.title);
					}
				}
			}
			if (titleArr.length === 0 || titleArr[titleArr.length - 1].toLowerCase() !== appContext.systemName.toLowerCase())
				titleArr.push(appContext.systemName);
			if (!routeSetsOwnTitle)
			{
				document.title = titleArr.join(" - ");
			}

			// Check session status
			// If the session is not active, the API framework will redirect us to login automatically.
			if (myApp.$store.state.sid)
			{
				//console.log("Route changed", from, to);
				// We use the route for a lot of UI state, so we should only check session status if the path has changed.
				if (from.path !== to.path)
				{
					ExecAPI("SessionStatus/IsSessionActive").catch(err => { });
				}
			}
		});
	});

	return router;
}