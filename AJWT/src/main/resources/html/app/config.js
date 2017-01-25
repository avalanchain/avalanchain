(function() {
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

    app.config(['commonConfigProvider', function(cfg) {
        cfg.config.controllerActivateSuccessEvent = config.events.controllerActivateSuccess;
        cfg.config.spinnerToggleEvent = config.events.spinnerToggle;
    }]);
    app.config(['$stateProvider', '$urlRouterProvider', '$ocLazyLoadProvider', '$locationProvider', '$httpProvider', '$breadcrumbProvider', routeConfigurator]);

    function routeConfigurator($stateProvider, $urlRouterProvider, $ocLazyLoadProvider, $locationProvider, $httpProvider, $breadcrumbProvider) {
        $breadcrumbProvider.setOptions({
            templateUrl: '/app/views/common/breadcrumb.html'
        });
        $urlRouterProvider.otherwise("/login");

        //$urlRouterProvider.deferIntercept();
        // $urlRouterProvider.otherwise("/accounts");
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
        }

        var clipboard = {
            files: ['/assets/js/plugins/ngclipboard/clipboard.min.js']
        };
        var clipboardng = {
            name: 'ngclipboard',
            files: ['/assets/js/plugins/ngclipboard/ngclipboard.min.js']
        };

        var codemirror ={
            serie: true,
                files: ['/assets/css/plugins/codemirror/codemirror.css','/assets/css/plugins/codemirror/ambiance.css','/assets/js/plugins/codemirror/codemirror.js','/assets/js/plugins/codemirror/mode/javascript/javascript.js']
        };
        var codemirrorui ={
            name: 'ui.codemirror',
                files: ['/assets/js/plugins/ui-codemirror/ui-codemirror.js']
        };


        $stateProvider

            .state('dashboards', {
                abstract: true,
                url: "/dashboards",
                templateUrl: "/app/views/common/content.html",
                data: {
                    permissions: {
                        only: 'isAuthorized',
                        redirectTo: 'login'
                    }
                },
            })
            .state('quoka', {
                abstract: true,
                url: "/quoka",
                templateUrl: "/app/views/common/content.html",
                data: {
                    permissions: {
                        only: 'isAuthorized',
                        redirectTo: 'login'
                    }
                },
            })
            .state('index', {
                abstract: true,
                // url: "/",
                templateUrl: "/app/views/common/content.html",
                data: {
                    permissions: {
                        only: 'isAuthorized',
                        redirectTo: 'login'
                    }
                },
                resolve: {
                    loadPlugin: function($ocLazyLoad) {
                        return $ocLazyLoad.load([{
                            insertBefore: '#loadBefore',
                            name: 'localytics.directives',
                            files: ['/assets/css/plugins/chosen/bootstrap-chosen.css', '/assets/js/plugins/chosen/chosen.jquery.js', '/assets/js/plugins/chosen/chosen.js']
                        },footable, footable_angular, clipboard, clipboardng]);
                    }
                }
            })
            .state('admin', {
                abstract: true,
                // url: "/",
                templateUrl: "/app/views/common/content.html",
                data: {
                    permissions: {
                        only: 'isAuthorized',
                        redirectTo: 'login'
                    }
                },
                resolve: {
                    loadPlugin: function($ocLazyLoad) {
                        return $ocLazyLoad.load([{
                            insertBefore: '#loadBefore',
                            name: 'localytics.directives',
                            files: ['/assets/css/plugins/chosen/bootstrap-chosen.css', '/assets/js/plugins/chosen/chosen.jquery.js', '/assets/js/plugins/chosen/chosen.js']
                        },footable, footable_angular, clipboard, clipboardng]);
                    }
                }
            })
            .state('dashboards.dashboard', {
                url: "/dashboard",
                templateUrl: "/app/views/dashboard/dashboard.html",
                data: {
                    pageTitle: 'dashboard'
                },
                resolve: {
                    loadPlugin: function($ocLazyLoad) {
                        return $ocLazyLoad.load([flot]);
                    }
                },
                ncyBreadcrumb: {
                    label: 'Dashboard',
                    text: 'Accounts'
                }
            })
            .state('index.chat', {
                url: "/chat",
                templateUrl: "/app/views/chat/chat.html",
                data: {
                    pageTitle: 'chat'
                },
                ncyBreadcrumb: {
                    label: 'Chat'
                }
            })
            .state('index.clusters', {
                url: "/clusters",
                templateUrl: "/app/views/clusters/clusters.html",
                data: {
                    pageTitle: 'clusters'
                },
                ncyBreadcrumb: {
                    label: 'Clusters'
                }
            })
            .state('index.cluster', {
                url: "/clusters/:clusterId",
                templateUrl: "/app/views/clusters/cluster.html",
                data: {
                    pageTitle: 'cluster'
                },
                resolve: {
                    loadPlugin: function($ocLazyLoad) {
                        return $ocLazyLoad.load([flot]);
                    }
                },
                ncyBreadcrumb: {
                    parent: function() {
                        return 'index.clusters';
                    },
                    label: 'Cluster'
                }
            })
            .state('index.accounts', {
                url: "/accounts",
                templateUrl: "/app/views/accounts/accounts.html",
                data: {
                    pageTitle: 'accounts'
                },
                ncyBreadcrumb: {
                    label: 'Accounts',
                    text: 'Accounts'
                }
            })
            .state('index.account', {
                url: "/accounts/:accountId",
                templateUrl: "/app/views/accounts/account.html",
                data: {
                    pageTitle: 'Account'
                },
                ncyBreadcrumb: {
                    parent: function() {
                        return 'index.accounts';
                    },
                    label: 'Account'
                }
            })
            .state('index.wallet', {
                url: "/accounts/wallet/:accountId",
                templateUrl: "/app/views/accounts/wallet.html",
                data: {
                    pageTitle: 'Wallet'
                },
                resolve: {
                    loadPlugin: function($ocLazyLoad) {
                        return $ocLazyLoad.load([c3chart, c3chartAngul]);
                    }
                },
                ncyBreadcrumb: {
                    parent: function() {
                        return 'index.accounts';
                    },
                    label: 'Wallet'
                }
            })
            .state('index.trader', {
                url: "/trader",
                templateUrl: "/app/views/trader/trader.html",
                data: {
                    pageTitle: 'trader'
                },
                ncyBreadcrumb: {
                    label: 'Trader'
                }
            })
            .state('index.transactions', {
                url: "/transactions",
                templateUrl: "/app/views/transactions/transactions.html",
                data: {
                    pageTitle: 'Transactions'
                },
                ncyBreadcrumb: {
                    label: 'Transactions'
                }
            })
            .state('index.nodes', {
                url: "/nodes",
                templateUrl: "/app/views/nodes/nodes.html",
                data: {
                    pageTitle: 'nodes'
                },
                ncyBreadcrumb: {
                    label: 'Nodes'
                }
            })
            .state('index.node', {
                url: "/nodes/:nodeId",
                templateUrl: "/app/views/nodes/node.html",
                data: {
                    pageTitle: 'node'
                },
                resolve: {
                    loadPlugin: function($ocLazyLoad) {
                        return $ocLazyLoad.load([flot]);
                    }
                },
                ncyBreadcrumb: {
                    parent: function() {
                        return 'index.nodes';
                    },
                    label: 'Node'
                }
            })
            .state('index.chains', {
                url: "/chains",
                templateUrl: "/app/views/chains/chains.html",
                data: {
                    pageTitle: 'Chains'
                },
                resolve: {
                    loadPlugin: function($ocLazyLoad) {
                        return $ocLazyLoad.load([codemirror, codemirrorui]);
                    }
                },
                ncyBreadcrumb: {
                    label: 'Chains'
                }
            })
            .state('admin.users', {
                url: "/users",
                templateUrl: "/app/views/users/users.html",
                data: {
                    pageTitle: 'users'
                },
                ncyBreadcrumb: {
                    label: 'Users'
                }
            })
            .state('admin.log', {
                url: "/log",
                templateUrl: "/app/views/log/log.html",
                data: {
                    pageTitle: 'Log'
                },
                ncyBreadcrumb: {
                    label: 'Log'
                }
            })
            .state('user', {
                abstract: true,
                url: "/user",
                templateUrl: "/app/views/user/layout.html",
            })
            .state('user.main', {
                url: "/dashboard",
                templateUrl: "/app/views/user/dashboard.html",
                data: {
                    pageTitle: 'user dashboard'
                }
            }).state('login', {
                url: "/login",
                templateUrl: "/app/views/common/login.html",
                data: {
                    pageTitle: 'user dashboard'
                }
            })
            // .state('index.doctor', {
            //     url: "/doctor",
            //     templateUrl: "/app/views/doctor/doctor.html",
            //     data: { pageTitle: 'doctor' }
            // })
            .state('quoka.dashboard', {
                url: "/dashboard",
                templateUrl: "/app/views/quoka/dashboard.html",
                data: {
                    pageTitle: 'Dashboard'
                },
                resolve: {
                    loadPlugin: function($ocLazyLoad) {
                        return $ocLazyLoad.load([
                            flot,
                            c3chart,
                            c3chartAngul, {
                                serie: true,
                                files: ['/assets/js/plugins/jvectormap/jquery-jvectormap-2.0.2.min.js', '/assets/js/plugins/jvectormap/jquery-jvectormap-2.0.2.css']
                            }, {
                                serie: true,
                                files: ['/assets/js/plugins/jvectormap/jquery-jvectormap-world-mill-en.js']
                            }, {
                                name: 'ui.checkbox',
                                files: ['/assets/js/bootstrap/angular-bootstrap-checkbox.js']
                            }
                        ]);
                    }
                },
                ncyBreadcrumb: {
                    label: 'Index Dashboard'
                }
            })
            .state('quoka.trader', {
                url: "/trader",
                templateUrl: "/app/views/quoka/trader.html",
                data: {
                    pageTitle: 'quoka trader'
                },
                ncyBreadcrumb: {
                    label: 'Index Trader'
                }
            })
    }

})();
