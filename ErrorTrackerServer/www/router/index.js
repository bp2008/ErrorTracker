import Vue from 'vue';
import VueRouter from 'vue-router';

import Login from 'appRoot/vues/Login.vue';
import PublicLayout from 'appRoot/vues/public/PublicLayout.vue';
import PublicHome from 'appRoot/vues/public/PublicHome.vue';
import ClientLayout from 'appRoot/vues/client/ClientLayout.vue';
import ClientHome from 'appRoot/vues/client/ClientHome.vue';
import ClientFiltersHome from 'appRoot/vues/client/filters/ClientFiltersHome.vue';

import AdminLayout from 'appRoot/vues/admin/AdminLayout.vue';
import AdminHome from 'appRoot/vues/admin/AdminHome.vue';
import AdminSettings from 'appRoot/vues/admin/AdminSettings.vue';
import AdminUsers from 'appRoot/vues/admin/AdminUsers.vue';
import AdminProjects from 'appRoot/vues/admin/AdminProjects.vue';

Vue.use(VueRouter);

export default function CreateRouter(store, basePath)
{
	const router = new VueRouter({
		mode: 'history',
		routes: [
			{
				path: basePath + '', component: PublicLayout,
				children: [
					{ path: '', component: PublicHome, name: 'publicHome' }
				]
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
							selectedEventIds: route.query.se ? route.query.se : ""
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
		});
	});

	return router;
}