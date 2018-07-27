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
            templateUrl: '/wwwroot/app/views/common/breadcrumb.html'
        });
        $urlRouterProvider.otherwise("/login");
        // $urlRouterProvider.otherwise("/dashboards/dashboard_investor");
        $ocLazyLoadProvider.config({
            // Set to true if you want to see what and when is dynamically loaded
            debug: false
        });
        var c3chart = {
            serie: true,
            files: ['/wwwroot/assets/css/plugins/c3/c3.min.css', '/wwwroot/assets/js/plugins/d3/d3.min.js', '/wwwroot/assets/js/plugins/c3/c3.min.js']
        };
        var c3chartAngul = {
            serie: true,
            name: 'gridshore.c3js.chart',
            files: ['/wwwroot/assets/js/plugins/c3/c3-angular.min.js']
        }

        var flot = {
            serie: true,
            name: 'angular-flot',
            files: ['/wwwroot/assets/js/plugins/flot/jquery.flot.js', '/wwwroot/assets/js/plugins/flot/jquery.flot.time.js', '/wwwroot/assets/js/plugins/flot/jquery.flot.tooltip.min.js', '/wwwroot/assets/js/plugins/flot/jquery.flot.spline.js',
                '/wwwroot/assets/js/plugins/flot/jquery.flot.resize.js', '/wwwroot/assets/js/plugins/flot/jquery.flot.pie.js', '/wwwroot/assets/js/plugins/flot/curvedLines.js', '/wwwroot/assets/js/plugins/flot/angular-flot.js',
            ]
        }

        var footable = {
            files: ['/wwwroot/assets/js/plugins/footable/footable.all.min.js', '/wwwroot/assets/css/plugins/footable/footable.core.css']
        };

        var footable_angular = {
            name: 'ui.footable',
            files: ['/wwwroot/assets/js/plugins/footable/angular-footable.js']
        };

        var touchspin = {
            files: ['/wwwroot/assets/css/plugins/touchspin/jquery.bootstrap-touchspin.min.css', '/wwwroot/assets/js/plugins/touchspin/jquery.bootstrap-touchspin.min.js']
        };

        var codemirror ={
            serie: true,
                files: ['/wwwroot/lib/codemirror/lib/codemirror.css','/wwwroot/lib/codemirror/theme/ambiance.css','/wwwroot/lib/codemirror/lib/codemirror.js','/wwwroot/lib/codemirror/mode/javascript/javascript.js']
        };
        var codemirrorui ={
            name: 'ui.codemirror',
                files: ['/wwwroot/lib/angular-ui-codemirror/ui-codemirror.js']
        };

        $stateProvider
            .state('dashboards',
            {
                abstract: true,
                url: "/dashboards",
                templateUrl: "/wwwroot/app/views/common/content.html",
            })
            .state('assets',
            {
                abstract: true,
                url: "/assets",
                templateUrl: "/wwwroot/app/views/common/content.html",
            })
            .state('exchange',
            {
                abstract: true,
                url: "/exchange",
                templateUrl: "/wwwroot/app/views/common/content.html",
            })
             .state('quoka', {
                 abstract: true,
                 url: "/quoka",
                 templateUrl: "/wwwroot/app/views/common/content.html",
             })
            .state('index',
            {
                abstract: true,
                // url: "/",
                templateUrl: "/wwwroot/app/views/common/content.html",
                resolve: {
                    loadPlugin: function ($ocLazyLoad) {
                        return $ocLazyLoad.load([
                            {
                                insertBefore: '#loadBefore',
                                name: 'localytics.directives',
                                files: [
                                    '/wwwroot/assets/css/plugins/chosen/bootstrap-chosen.css',
                                    '/wwwroot/assets/js/plugins/chosen/chosen.jquery.js',
                                    '/wwwroot/assets/js/plugins/chosen/chosen.js'
                                ]
                            }, footable, footable_angular
                        ]);
                    }
                }
            })
            .state('wallet', {
                abstract: true,
                url: "/wallet",
                templateUrl: "/wwwroot/app/views/common/topcontent.html",
            })
            .state('dashboards.dashboard',
            {
                url: "/dashboard",
                templateUrl: "/wwwroot/app/views/dashboard/dashboard.html",
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
                templateUrl: "/wwwroot/app/views/dashboard/dashboard_investor.html",
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
                templateUrl: "/wwwroot/app/views/chat/chat.html",
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
                templateUrl: "/wwwroot/app/views/clusters/clusters.html",
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
                templateUrl: "/wwwroot/app/views/clusters/cluster.html",
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
                templateUrl: "/wwwroot/app/views/accounts/accounts.html",
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
                templateUrl: "/wwwroot/app/views/accounts/account.html",
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
            .state('wallet.wallet',
            {
                url: "/account",
                templateUrl: "/wwwroot/app/views/accounts/wallet.html",
                data: {
                    pageTitle: 'Wallet'
                },
                resolve: {
                    loadPlugin: function ($ocLazyLoad) {
                        return $ocLazyLoad.load([c3chart, c3chartAngul]);
                    }
                },
                // ncyBreadcrumb: {
                //     parent: function () {
                //         return 'index.accounts';
                //     },
                //     label: 'Wallet'
                // }
            })
            .state('wallet.transactions',
            {
                url: "/transactions",
                templateUrl: "/wwwroot/app/views/accounts/transactions.html",
                data: {
                    pageTitle: 'Transactions'
                },
                resolve: {
                    loadPlugin: function ($ocLazyLoad) {
                        return $ocLazyLoad.load([c3chart, c3chartAngul]);
                    }
                },
                // ncyBreadcrumb: {
                //     parent: function () {
                //         return 'index.accounts';
                //     },
                //     label: 'Wallet'
                // }
            })
            
            .state('wallet.bridge',
            {
                url: "/bridge",
                templateUrl: "/wwwroot/app/views/accounts/bridge.html",
                data: {
                    pageTitle: 'Bridge'
                },
                resolve: {
                    loadPlugin: function ($ocLazyLoad) {
                        return $ocLazyLoad.load([c3chart, c3chartAngul]);
                    }
                },
                // ncyBreadcrumb: {
                //     parent: function () {
                //         return 'index.accounts';
                //     },
                //     label: 'Wallet'
                // }
            }).state('index.transactions',
            {
                url: "/transactions",
                templateUrl: "/wwwroot/app/views/transactions/transactions.html",
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
                templateUrl: "/wwwroot/app/views/nodes/nodes.html",
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
                templateUrl: "/wwwroot/app/views/nodes/node.html",
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
                templateUrl: "/wwwroot/app/views/streams/streams.html",
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
            .state('index.chains2', {
                url: "/chains2",
                templateUrl: "/wwwroot/app/views/chains/chains.html",
                data: {
                    pageTitle: 'Chains'
                },
                resolve: {
                    loadPlugin: function($ocLazyLoad) {
                        return $ocLazyLoad.load([codemirror, codemirrorui]);
                    }
                },
                ncyBreadcrumb: {
                    label: 'Chains2'
                }
            })
            .state('assets.createassets',
            {
                url: "/createassets",
                templateUrl: "/wwwroot/app/views/assets/createassets.html",
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
                templateUrl: "/wwwroot/app/views/assets/assets.html",
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
                templateUrl: "/wwwroot/app/views/admin/admin.html",
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
                templateUrl: "/wwwroot/app/views/user/layout.html",
            })
            .state('user.main',
            {
                url: "/dashboard",
                templateUrl: "/wwwroot/app/views/user/dashboard.html",
                data: {
                    pageTitle: 'user dashboard'
                }
            }).state('login',
            {
                url: "/login",
                templateUrl: "/wwwroot/app/views/common/login.html",
                data: {
                    pageTitle: 'user dashboard'
                }
            })
            .state('exchange.traderbot',
            {
                url: "/traderbot",
                templateUrl: "/wwwroot/app/views/exchange/traderbot.html",
                data: {
                    pageTitle: 'Exchange'
                },
                ncyBreadcrumb: {
                    label: 'Trader Bot'
                }
            })
            .state('exchange.dashboard',
            {
                url: "/dashboard",
                templateUrl: "/wwwroot/app/views/exchange/dashboard.html",
                data: {
                    pageTitle: 'Exchange'
                },
                ncyBreadcrumb: {
                    label: 'Dashboard'
                },
                resolve: {
                    loadPlugin: function ($ocLazyLoad) {
                        return $ocLazyLoad.load([
                            {
                                files: ['/wwwroot/lib/jquery-sparkline/dist/jquery.sparkline.min.js']
                            }
                        ]);
                    }
                }
            }).state('exchange.trader',
            {
                url: "/trade",
                templateUrl: "/wwwroot/app/views/exchange/trader.html",
                data: {
                    pageTitle: 'Exchange'
                },
                ncyBreadcrumb: {
                    label: 'Trader'
                }
            }).state('exchange.symbol',
            {
                url: "/:symbol",
                templateUrl: "/wwwroot/app/views/exchange/symbol.html",
                data: {
                    pageTitle: 'Currency'
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
                    templateUrl: "/wwwroot/app/views/quoka/trader.html",
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
                    templateUrl: "/wwwroot/app/views/quoka/dashboard.html",
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