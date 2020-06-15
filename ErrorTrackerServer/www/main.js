import 'babel-polyfill'; // babel-polyfill must be the first import!

import EventBus from 'appRoot/scripts/EventBus';

import Vue from 'vue';
import App from 'appRoot/vues/App.vue';
import CreateStore from 'appRoot/store/store';
import CreateRouter from 'appRoot/router/index';


import vsvg from 'appRoot/vues/common/controls/VSvg.vue';
Vue.component('vsvg', vsvg);

import '@deveodk/vue-toastr/dist/@deveodk/vue-toastr.css';
import VueToastr from '@deveodk/vue-toastr';
Vue.use(VueToastr);

import VueGoodTablePlugin from 'vue-good-table'; // this is an enormous dependency, and could be removed in order to reduce bundle size
import 'vue-good-table/dist/vue-good-table.css'; // ^^
Vue.use(VueGoodTablePlugin);

import FolderNode from 'appRoot/vues/client/projectdisplay/folder/FolderNode.vue';
Vue.component('FolderNode', FolderNode);

import ScaleLoader from 'appRoot/vues/common/ScaleLoader.vue';
Vue.component('ScaleLoader', ScaleLoader);

import VueVirtualScroller from 'vue-virtual-scroller';
Vue.use(VueVirtualScroller);
// Any recursively nested components must be globally registered here
//Vue.component('Example', require('Example.vue').default);

let store = window.store = CreateStore();
let router = window.router = CreateRouter(store, appContext.appPath);
let myApp = window.myApp = new Vue({
	store,
	router,
	...App
});

import ToasterHelper from 'appRoot/scripts/ToasterHelper';
window.toaster = new ToasterHelper(myApp.$toastr);

import * as Util from 'appRoot/scripts/Util';
window.Util = Util;

let uidCounter = 0;
window.GetUid = function ()
{
	return uidCounter++;
};

router.onReady(() =>
{
	const matchedComponents = router.getMatchedComponents();

	if (matchedComponents.length < 1)
		window.location.replace(appContext.appPath + "404.html");

	myApp.$mount('#app');
});