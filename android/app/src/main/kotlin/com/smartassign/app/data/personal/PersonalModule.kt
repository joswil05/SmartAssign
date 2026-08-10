package com.smartassign.app.data.personal

import dagger.Binds
import dagger.Module
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import javax.inject.Singleton

@Module
@InstallIn(SingletonComponent::class)
abstract class PersonalModule {
    @Binds
    @Singleton
    abstract fun personalRepositorio(impl: PersonalRepositorioImpl): PersonalRepositorio
}
