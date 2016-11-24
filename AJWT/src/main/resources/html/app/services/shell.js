(function () {
    'use strict';

    var controllerId = 'shell';
    angular.module('avalanchain').controller(controllerId,
        ['$rootScope', '$window', 'common', 'config', shell]);

    function shell($rootScope, $window, common, config) {
        var vm = this;
        var logSuccess = common.logger.getLogFn(controllerId, 'success');
        var events = config.events;
        vm.busyMessage = 'Please wait ...';
        vm.isBusy = true;
        vm.spinnerOptions = {
            radius: 20,
            lines: 7,
            length: 0,
            width: 20,
            speed: 1.7,
            corners: 1.0,
            trail: 100,
            color: '#ffffff'
        };

        activate();

        //$rootScope.$on('$locationChangeStart',
        //    function (event,current,previous) {
        //        var answer = $window.confirm('Leave?');
        //        if (!answer) {
        //            event.preventDefault();
        //            return;
        //        }

        //    })

        function activate() {
        //    logSuccess('Fish mbb App loaded!', null, true);
        //    common.activateController([], controllerId);
        }

        function toggleSpinner(on) {
             vm.isBusy = on;
        }

        $rootScope.$on('$stateChangeStart',
            function(event, toState, toParams, fromState, fromParams) {
                 toggleSpinner(true);
            }
        );

        $rootScope.$on('$stateChangeSuccess',
            function(event, toState, toParams, fromState, fromParams) {
                 toggleSpinner(false);
            }
        );

        //vm.Login = function () {
        //    adalService.login();
        //};
        //vm.Logout = function () {
        //    adalService.logOut();
        //};

        $rootScope.$on(events.contr,
            function(data) {
                 toggleSpinner(false);
            }
        );

        //$rootScope.$on(events.controllerActivateSuccess,
        //    function(data) {
        //         toggleSpinner(false);
        //    }
        //);

        $rootScope.$on(events.spinnerToggle,
            function(event, data) {
                 toggleSpinner(data.show);
            }
        );
    };
})();
