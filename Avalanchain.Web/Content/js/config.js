/// <reference path="../views/clusters/clusters.html" />
/**
 * avalanchain - Responsive Admin Theme
 * Copyright 2015 Webapplayers.com
 *
 * avalanchain theme use AngularUI Router to manage routing and views
 * Each view are defined as state.
 * Initial there are written state for all view in theme.
 *
 */
function config($stateProvider, $urlRouterProvider, $ocLazyLoadProvider, $locationProvider, adalAuthenticationServiceProvider, $httpProvider) {
    //$locationProvider.html5Mode({
    //    enabled: true,
    //    requireBase: false
    //});
    //$locationProvider.html5Mode(true).hashPrefix('!');
    $urlRouterProvider.otherwise("/dashboards/dashboard");

    $ocLazyLoadProvider.config({
        // Set to true if you want to see what and when is dynamically loaded
        debug: false
    });

    var endpoints = {
        "http://localhost:48213": "https://avlnchdemo.azurewebsites.net"
        // Map the location of a request to an API to a the identifier of the associated resource
        //"Enter the root location of your To Go API here, e.g. https://contosotogo.azurewebsites.net/":
        //    "Enter the App ID URI of your To Go API here, e.g. https://contoso.onmicrosoft.com/ToGoAPI",
    };

    //adalAuthenticationServiceProvider.init(
    //    {
    //        instance: 'https://login.microsoftonline.com/',
    //        tenant: 'safytrack.onmicrosoft.com',
    //        clientId: '02fa79b9-d917-4496-93d4-8fb311611f51',
    //        extraQueryParameter: 'nux=1',
    //        //endpoints: endpoints,
    //        //cacheLocation: 'localStorage', // enable this for IE, as sessionStorage does not work for localhost.
    //    },
    //    $httpProvider
    //    );

    $stateProvider

        .state('dashboards', {
            abstract: true,
            url: "/dashboards",
            templateUrl: "/Content/views/common/content.html",
        })
        .state('quoka', {
            abstract: true,
            url: "/quoka",
            templateUrl: "/Content/views/common/content.html",
        })
        .state('index', {
            abstract: true,
            url: "/index",
            templateUrl: "/Content/views/common/content.html",
        })
        .state('dashboards.dashboard', {
            url: "/dashboard",
            templateUrl: "/Content/views/dashboard/dashboard.html",
            data: { pageTitle: 'dashboard' },

            //requireADLogin: true,
            resolve: {
                loadPlugin: function ($ocLazyLoad) {
                    return $ocLazyLoad.load([
                        {
                            serie: true,
                            name: 'angular-flot',
                            files: ['/Content/js/plugins/flot/jquery.flot.js', '/Content/js/plugins/flot/jquery.flot.time.js', '/Content/js/plugins/flot/jquery.flot.tooltip.min.js', '/Content/js/plugins/flot/jquery.flot.spline.js', '/Content/js/plugins/flot/jquery.flot.resize.js', '/Content/js/plugins/flot/jquery.flot.pie.js', '/Content/js/plugins/flot/curvedLines.js', '/Content/js/plugins/flot/angular-flot.js', ]
                        }
                    ]);
                }
            }
        })
        .state('quoka.dashboard', {
            url: "/dashboard",
            templateUrl: "/Content/views/quoka/dashboard.html",
            data: { pageTitle: 'Dashboard' },
            resolve: {
                loadPlugin: function ($ocLazyLoad) {
                    return $ocLazyLoad.load([
                        {
                            serie: true,
                            name: 'angular-flot',
                            files: ['/Content/js/plugins/flot/jquery.flot.js', '/Content/js/plugins/flot/jquery.flot.time.js', '/Content/js/plugins/flot/jquery.flot.tooltip.min.js', '/Content/js/plugins/flot/jquery.flot.spline.js', '/Content/js/plugins/flot/jquery.flot.resize.js', '/Content/js/plugins/flot/jquery.flot.pie.js', '/Content/js/plugins/flot/curvedLines.js', '/Content/js/plugins/flot/angular-flot.js', ]
                        },
                        {
                            serie: true,
                            files: ['/Content/js/plugins/jvectormap/jquery-jvectormap-2.0.2.min.js', '/Content/js/plugins/jvectormap/jquery-jvectormap-2.0.2.css']
                        },
                        {
                            serie: true,
                            files: ['/Content/js/plugins/jvectormap/jquery-jvectormap-world-mill-en.js']
                        },
                        {
                            name: 'ui.checkbox',
                            files: ['/Content/js/bootstrap/angular-bootstrap-checkbox.js']
                        },
                        {
                            serie: true,
                            files: ['/Content/css/plugins/c3/c3.min.css', '/Content/js/plugins/d3/d3.min.js', '/Content/js/plugins/c3/c3.min.js']
                        },
                        {
                            serie: true,
                            name: 'gridshore.c3js.chart',
                            files: ['/Content/js/plugins/c3/c3-angular.min.js']
                        }
                    ]);
                }
            }
        })
        .state('quoka.trader', {
            url: "/trader",
            templateUrl: "/Content/views/quoka/trader.html",
            data: { pageTitle: 'quoka trader' }
        })
        .state('index.clusters', {
            url: "/clusters",
            templateUrl: "/Content/views/clusters/clusters.html",
            data: { pageTitle: 'clusters' }
        })
        .state('index.accounts', {
            url: "/accounts",
            templateUrl: "/Content/views/accounts/accounts.html",
            data: { pageTitle: 'accounts' }
        })
        .state('index.trader', {
            url: "/trader",
            templateUrl: "/Content/views/trader/trader.html",
            data: { pageTitle: 'trader' }
        })
        .state('index.nodes', {
            url: "/nodes",
            templateUrl: "/Content/views/nodes/nodes.html",
            data: { pageTitle: 'nodes' }
        })
        .state('index.streams', {
            url: "/streams",
            templateUrl: "/Content/views/streams/streams.html",
            data: { pageTitle: 'streams' }
        })
        .state('index.doctor', {
            url: "/doctor",
            templateUrl: "/Content/views/doctor/doctor.html",
            data: { pageTitle: 'doctor' }
        })
    .state('index.admin', {
        url: "/admin",
        templateUrl: "/Content/views/admin/admin.html",
        data: { pageTitle: 'admin' }
    });
        //.state('index.main', {
        //    url: "/main",
        //    templateUrl: "/Content/views/main.html",
        //    data: { pageTitle: 'Example view' }
        //})
        //.state('index.minor', {
        //    url: "/minor",
        //    templateUrl: "/Content/views/minor.html",
        //    data: { pageTitle: 'Example view' }
        //})
}
angular
    .module('avalanchain')
    .config(config)
    .run(function ($rootScope, $state) {
        $rootScope.$state = $state;
    });
