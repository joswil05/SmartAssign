package com.smartassign.app.data.asignacion

import dagger.Binds
import dagger.Module
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import javax.inject.Singleton

@Module
@InstallIn(SingletonComponent::class)
abstract class AsignacionModule {
    @Binds
    @Singleton
    abstract fun asignacionRepositorio(impl: AsignacionRepositorioImpl): AsignacionRepositorio
}
