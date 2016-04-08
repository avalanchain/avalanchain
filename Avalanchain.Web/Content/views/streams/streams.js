(function () {
    'use strict';
    var controllerId = 'Streams';
    angular.module('avalanchain').controller(controllerId, ['common', streams]);

    function streams(common) {
        var getLogFn = common.logger.getLogFn;
        var log = getLogFn(controllerId);

        this.info = 'Streams';
        this.helloText = 'Welcome in Avalanchain';
        this.descriptionText = 'CASCADING REACTIVE BLOCKCHAINS';

        activate();

        function activate() {
            common.activateController([], controllerId)
                .then(function () { log('Activated Strems') });//log('Activated Admin View');
        }
    };


})();