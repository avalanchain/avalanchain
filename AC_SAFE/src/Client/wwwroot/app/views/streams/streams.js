
(function() {
    'use strict';
    var controllerId = 'streams';
    angular.module('avalanchain').controller(controllerId, ['common', '$uibModal', 'dataservice', 'jwtservice', streams]);

    function streams(common, $uibModal, dataservice, jwtservice) {
        var getLogFn = common.logger.getLogFn;
        var log = getLogFn(controllerId);
        var vm = this;
        vm.encode = 'true';
        // vm.decode = !vm.encode;
        vm.maxSize = 5;
        vm.totalItems = [];
        vm.page = 1;

        // var jwS = jwtservice.generateJWS();
        // var isValidjws = jwtservice.verifyJWS(jwS);
        //
        // var jwTDecode = jwtservice.generateJWT();
        // var isValidjwt = jwtservice.verifyJWS(jwTDecode);
        //
        // var jwtencode = jwtservice.getJWT(jwTDecode);

        // vm.showCluster = function(cluster) {
        //     $state.go('index.cluster', {
        //         clusterId: cluster.id
        //     });
        // }
        vm.test = function() {
            var tt = vm.encode;
              var tt = vm.decode;
        }
        activate();

        function activate() {
            common.activateController([getData()], controllerId)
                .then(function() {
                    log('Activated Chains')
                }); //log('Activated Admin View');
        }

        function getData() {
            dataservice.getData().then(function(data) {
                vm.data = data;
                vm.streams = vm.data.streams;
                vm.tokens = vm.streams.map(function(stream) {
                  var jwTDecode = jwtservice.generateJWT(stream);
                  var token = {
                    token : jwTDecode,
                    verified : jwtservice.verifyJWT(jwTDecode, stream.publicKey)
                  }
                    return token;
                });
                //var jwS = jwtservice.generateJWS(vm.streams[0]);
                // var jwTDecode = jwtservice.generateJWT(vm.streams[0]);
                // var isValidjwt = jwtservice.verifyJWS(jwTDecode);
                //
                // var jwtencode = jwtservice.getJWT(jwTDecode);
            });

        }

    };


})();
