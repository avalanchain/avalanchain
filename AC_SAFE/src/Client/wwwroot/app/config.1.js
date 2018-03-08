(function () {
    'use strict';

    var app = angular.module('avalanchain');
    //
    var events = {
        controllerActivateSuccess: 'controller.activateSuccess',
        spinnerToggle: 'spinner.toggle'
    };

    var config = {
        appErrorPrefix: '[HT Error] ', //Configure the exceptionHandler decorator
        docTitle: 'avalanchain: ',
        events: events,
        version: '0.8.1'
    };

    app.value('config', config);

    app.config(['commonConfigProvider', function (cfg) {
        cfg.config.controllerActivateSuccessEvent = config.events.controllerActivateSuccess;
        cfg.config.spinnerToggleEvent = config.events.spinnerToggle;
    }]);
    app.config(['$stateProvider', '$urlRouterProvider', '$ocLazyLoadProvider', '$locationProvider', '$httpProvider', '$breadcrumbProvider', routeConfigurator]);

    function routeConfigurator($stateProvider, $urlRouterProvider, $ocLazyLoadProvider, $locationProvider, $httpProvider, $breadcrumbProvider) {
        $breadcrumbProvider.setOptions({
            templateUrl: '/app/views/common/breadcrumb.html'
        });
        $urlRouterProvider.otherwise("/login");
        // $urlRouterProvider.otherwise("/dashboards/dashboard_investor");
        $ocLazyLoadProvider.config({
            // Set to true if you want to see what and when is dynamically loaded
            debug: false
        });
        var c3chart = {
            serie: true,
            files: ['/assets/css/plugins/c3/c3.min.css', '/assets/js/plugins/d3/d3.min.js', '/assets/js/plugins/c3/c3.min.js']
        };
        var c3chartAngul = {
            serie: true,
            name: 'gridshore.c3js.chart',
            files: ['/assets/js/plugins/c3/c3-angular.min.js']
        }

        var flot = {
            serie: true,
            name: 'angular-flot',
            files: ['/assets/js/plugins/flot/jquery.flot.js', '/assets/js/plugins/flot/jquery.flot.time.js', '/assets/js/plugins/flot/jquery.flot.tooltip.min.js', '/assets/js/plugins/flot/jquery.flot.spline.js',
                '/assets/js/plugins/flot/jquery.flot.resize.js', '/assets/js/plugins/flot/jquery.flot.pie.js', '/assets/js/plugins/flot/curvedLines.js', '/assets/js/plugins/flot/angular-flot.js',
            ]
        }

        var footable = {
            files: ['/assets/js/plugins/footable/footable.all.min.js', '/assets/css/plugins/footable/footable.core.css']
        };

        var footable_angular = {
            name: 'ui.footable',
            files: ['/assets/js/plugins/footable/angular-footable.js']
        };

        var touchspin = {
            files: ['/assets/css/plugins/touchspin/jquery.bootstrap-touchspin.min.css', '/assets/js/plugins/touchspin/jquery.bootstrap-touchspin.min.js']
        };


        $stateProvider
            .state('dashboards',
            {
                abstract: true,
                url: "/dashboards",
                templateUrl: "/app/views/common/content.html",
            })
            .state('assets',
            {
                abstract: true,
                url: "/assets",
                templateUrl: "/app/views/common/content.html",
            })
            .state('exchange',
            {
                abstract: true,
                url: "/exchange",
                templateUrl: "/app/views/common/content.html",
            })
             .state('quoka', {
                 abstract: true,
                 url: "/quoka",
                 templateUrl: "/app/views/common/content.html",
             })
            .state('index',
            {
                abstract: true,
                // url: "/",
                templateUrl: "/app/views/common/content.html",
                resolve: {
                    loadPlugin: function ($ocLazyLoad) {
                        return $ocLazyLoad.load([
                            {
                                insertBefore: '#loadBefore',
                                name: 'localytics.directives',
                                files: [
                                    '/assets/css/plugins/chosen/bootstrap-chosen.css',
                                    '/assets/js/plugins/chosen/chosen.jquery.js',
                                    '/assets/js/plugins/chosen/chosen.js'
                                ]
                            }, footable, footable_angular
                        ]);
                    }
                }
            })
            .state('dashboards.dashboard',
            {
                url: "/dashboard",
                templateUrl: "/app/views/dashboard/dashboard.html",
                data: {
                    pageTitle: 'Dashboard'
                },
                resolve: {
                    loadPlugin: function ($ocLazyLoad) {
                        return $ocLazyLoad.load([flot]);
                    }
                },
                ncyBreadcrumb: {
                    label: 'Dashboard',
                    text: 'Accounts'
                }
            })
            .state('dashboards.dashboard_investor',
            {
                url: "/dashboard_investor",
                templateUrl: "/app/views/dashboard/dashboard_investor.html",
                data: {
                    pageTitle: 'INVESTOR'
                },
                resolve: {
                    loadPlugin: function ($ocLazyLoad) {
                        return $ocLazyLoad.load([flot, touchspin]);
                    }
                },
                ncyBreadcrumb: {
                    label: 'Dashboard',
                    text: 'Accounts'
                }
            })
            .state('index.chat',
            {
                url: "/chat",
                templateUrl: "/app/views/chat/chat.html",
                data: {
                    pageTitle: 'Chat'
                },
                ncyBreadcrumb: {
                    label: 'Chat'
                }
            })
            .state('index.clusters',
            {
                url: "/clusters",
                templateUrl: "/app/views/clusters/clusters.html",
                data: {
                    pageTitle: 'Clusters'
                },
                ncyBreadcrumb: {
                    label: 'Clusters'
                }
            })
            .state('index.cluster',
            {
                url: "/clusters/:clusterId",
                templateUrl: "/app/views/clusters/cluster.html",
                data: {
                    pageTitle: 'Cluster'
                },
                resolve: {
                    loadPlugin: function ($ocLazyLoad) {
                        return $ocLazyLoad.load([flot]);
                    }
                },
                ncyBreadcrumb: {
                    parent: function () {
                        return 'index.clusters';
                    },
                    label: 'Cluster'
                }
            })
            .state('index.accounts',
            {
                url: "/accounts",
                templateUrl: "/app/views/accounts/accounts.html",
                data: {
                    pageTitle: 'Accounts'
                },
                ncyBreadcrumb: {
                    label: 'Accounts',
                    text: 'Accounts'
                }
            })
            .state('index.account',
            {
                url: "/accounts/:accountId",
                templateUrl: "/app/views/accounts/account.html",
                data: {
                    pageTitle: 'Account'
                },
                ncyBreadcrumb: {
                    parent: function () {
                        return 'index.accounts';
                    },
                    label: 'Account'
                }
            })
            .state('index.wallet',
            {
                url: "/accounts/wallet/:accountId",
                templateUrl: "/app/views/accounts/wallet.html",
                data: {
                    pageTitle: 'Wallet'
                },
                resolve: {
                    loadPlugin: function ($ocLazyLoad) {
                        return $ocLazyLoad.load([c3chart, c3chartAngul]);
                    }
                },
                ncyBreadcrumb: {
                    parent: function () {
                        return 'index.accounts';
                    },
                    label: 'Wallet'
                }
            })
            .state('index.transactions',
            {
                url: "/transactions",
                templateUrl: "/app/views/transactions/transactions.html",
                data: {
                    pageTitle: 'Transactions'
                },
                ncyBreadcrumb: {
                    label: 'Transactions'
                }
            })
            .state('index.nodes',
            {
                url: "/nodes",
                templateUrl: "/app/views/nodes/nodes.html",
                data: {
                    pageTitle: 'Nodes'
                },
                ncyBreadcrumb: {
                    label: 'Nodes'
                }
            })
            .state('index.node',
            {
                url: "/nodes/:nodeId",
                templateUrl: "/app/views/nodes/node.html",
                data: {
                    pageTitle: 'Node'
                },
                resolve: {
                    loadPlugin: function ($ocLazyLoad) {
                        return $ocLazyLoad.load([flot]);
                    }
                },
                ncyBreadcrumb: {
                    parent: function () {
                        return 'index.nodes';
                    },
                    label: 'Node'
                }
            })
            .state('index.chains',
            {
                url: "/chains",
                templateUrl: "/app/views/streams/streams.html",
                data: {
                    pageTitle: 'Chains'
                },
                // resolve: {
                //     loadPlugin: function($ocLazyLoad) {
                //         return $ocLazyLoad.load([footable, footable_angular]);
                //     }
                // },
                ncyBreadcrumb: {
                    label: 'Chains'
                }
            })
            .state('assets.createassets',
            {
                url: "/createassets",
                templateUrl: "/app/views/assets/createassets.html",
                data: {
                    pageTitle: 'Create Assets'
                },
                ncyBreadcrumb: {
                    label: 'Create',
                },
                resolve: {
                    loadPlugin: function ($ocLazyLoad) {
                        return $ocLazyLoad.load([touchspin]);
                    }
                },
            })
            .state('assets.assets',
            {
                url: "/assets",
                templateUrl: "/app/views/assets/assets.html",
                data: {
                    pageTitle: 'Assets'
                },
                ncyBreadcrumb: {
                    label: 'All Assets',
                },
                resolve: {
                    loadPlugin: function ($ocLazyLoad) {
                        return $ocLazyLoad.load([touchspin]);
                    }
                },
            })
            .state('index.admin',
            {
                url: "/admin",
                templateUrl: "/app/views/admin/admin.html",
                data: {
                    pageTitle: 'Admin'
                },
                ncyBreadcrumb: {
                    label: 'Settings'
                }
            })
            .state('user',
            {
                abstract: true,
                url: "/user",
                templateUrl: "/app/views/user/layout.html",
            })
            .state('user.main',
            {
                url: "/dashboard",
                templateUrl: "/app/views/user/dashboard.html",
                data: {
                    pageTitle: 'user dashboard'
                }
            }).state('login',
            {
                url: "/login",
                templateUrl: "/app/views/common/login.html",
                data: {
                    pageTitle: 'user dashboard'
                }
            })
            .state('exchange.trader',
            {
                url: "/trader",
                templateUrl: "/app/views/exchange/trader.html",
                data: {
                    pageTitle: 'Exchange'
                },
                ncyBreadcrumb: {
                    label: 'Trader'
                }
            })
            .state('exchange.dashboard',
            {
                url: "/dashboard",
                templateUrl: "/app/views/exchange/dashboard.html",
                data: {
                    pageTitle: 'Exchange'
                },
                ncyBreadcrumb: {
                    label: 'Dashboard'
                }
            }).state('exchange.symbol',
            {
                url: "/:symbol",
                templateUrl: "/app/views/exchange/symbol.html",
                data: {
                    pageTitle: 'Symbol'
                },
                controller: function ($scope, $stateParams) {
                    $scope.foo = $stateParams.symbol || '';
                },
                ncyBreadcrumb: {
                    parent: function () {
                        return 'exchange.dashboard';
                    },
                    label: '{{foo}}'
                }
            }).state('quoka.trader',
                {
                    url: "/trader",
                    templateUrl: "/app/views/quoka/trader.html",
                    data: {
                        pageTitle: 'Exchange'
                    },
                    ncyBreadcrumb: {
                        label: 'Trader'
                    }
                })
            .state('quoka.dashboard',
                {
                    url: "/dashboard",
                    templateUrl: "/app/views/quoka/dashboard.html",
                    data: {
                        pageTitle: 'Exchange'
                    }, resolve: {
                        loadPlugin: function ($ocLazyLoad) {
                            return $ocLazyLoad.load([c3chart, c3chartAngul, flot]);
                        }
                    },
                    ncyBreadcrumb: {
                        label: 'Dashboard'
                    }
                });
    }

})();